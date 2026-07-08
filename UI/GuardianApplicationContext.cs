using System.Diagnostics;
using CodexGuardian.Models;
using CodexGuardian.Services;

namespace CodexGuardian.UI;

public sealed class GuardianApplicationContext : ApplicationContext
{
    private readonly SettingsStore _settingsStore;
    private readonly AppLogger _logger;
    private readonly AutoStartManager _autoStartManager;
    private readonly CodexProcessManager _codexProcessManager;
    private readonly WatchdogCoordinator _watchdogCoordinator;
    private readonly NotifyIcon _notifyIcon;
    private readonly System.Windows.Forms.Timer _timer;
    private AppSettings _settings;
    private SettingsForm? _settingsForm;

    public GuardianApplicationContext(SettingsStore settingsStore, AppSettings settings, int recoveredCount)
    {
        _settingsStore = settingsStore;
        _settings = settings;
        _logger = new AppLogger(_settingsStore.LogFilePath);
        _autoStartManager = new AutoStartManager(_logger);
        _codexProcessManager = new CodexProcessManager(_settings, _logger);
        _watchdogCoordinator = new WatchdogCoordinator(_settings, _logger, recoveredCount);
        _notifyIcon = CreateNotifyIcon();
        _timer = new System.Windows.Forms.Timer { Interval = 2000 };

        _codexProcessManager.StateChanged += (_, _) => UpdateTrayText();
        _timer.Tick += (_, _) => _codexProcessManager.Tick();

        ApplyAutoStart();
        _watchdogCoordinator.Start();
        _notifyIcon.Visible = true;
        _codexProcessManager.StartInitialIfNeeded();
        _timer.Start();

        if (_settings.ShowNotifications)
        {
            _notifyIcon.ShowBalloonTip(2000, "Codex Guardian", "Codex の守護を開始しました。", ToolTipIcon.Info);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _timer.Dispose();
            _notifyIcon.Dispose();
            _watchdogCoordinator.Dispose();
        }

        base.Dispose(disposing);
    }

    private NotifyIcon CreateNotifyIcon()
    {
        var menu = new ContextMenuStrip();
        var guardToggle = new ToolStripMenuItem("守護を無効にする")
        {
            Checked = _settings.EnableGuardian
        };
        guardToggle.Click += (_, _) => ToggleGuardian();

        var startCodex = new ToolStripMenuItem("Codex を起動");
        startCodex.Click += (_, _) => _codexProcessManager.StartCodex("手動起動");

        var restartCodex = new ToolStripMenuItem("Codex を再起動");
        restartCodex.Click += (_, _) => _codexProcessManager.RestartCodex();

        var openSettings = new ToolStripMenuItem("設定");
        openSettings.Click += (_, _) => ShowSettings();

        var openLogs = new ToolStripMenuItem("ログを開く");
        openLogs.Click += (_, _) => OpenLogDirectory();

        var exit = new ToolStripMenuItem("終了");
        exit.Click += (_, _) => ExitApplication();

        menu.Items.AddRange(new ToolStripItem[]
        {
            guardToggle,
            new ToolStripSeparator(),
            startCodex,
            restartCodex,
            new ToolStripSeparator(),
            openSettings,
            openLogs,
            new ToolStripSeparator(),
            exit
        });

        var notifyIcon = new NotifyIcon
        {
            Icon = LoadAppIcon(),
            ContextMenuStrip = menu,
            Text = "Codex Guardian",
            Visible = false
        };
        notifyIcon.DoubleClick += (_, _) => ShowSettings();
        return notifyIcon;
    }

    private Icon LoadAppIcon()
    {
        var assetIcon = Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico");
        if (File.Exists(assetIcon))
        {
            return new Icon(assetIcon);
        }

        return Icon.ExtractAssociatedIcon(Application.ExecutablePath) ?? SystemIcons.Application;
    }

    private void ToggleGuardian()
    {
        _settings.EnableGuardian = !_settings.EnableGuardian;
        SaveAndApplySettings();
        UpdateTrayMenu();
        Notify(_settings.EnableGuardian ? "Codex の守護を有効にしました。" : "Codex の守護を無効にしました。");
    }

    private void ShowSettings()
    {
        if (_settingsForm is { IsDisposed: false })
        {
            _settingsForm.Activate();
            return;
        }

        _settingsForm = new SettingsForm(_settings);
        _settingsForm.SettingsSaved += (_, updated) =>
        {
            _settings = updated;
            SaveAndApplySettings();
        };
        _settingsForm.Show();
        _settingsForm.Activate();
    }

    private void SaveAndApplySettings()
    {
        _settingsStore.Save(_settings);
        _codexProcessManager.ApplySettings(_settings);
        _watchdogCoordinator.ApplySettings(_settings);
        ApplyAutoStart();
        UpdateTrayMenu();
        UpdateTrayText();
    }

    private void ApplyAutoStart()
    {
        try
        {
            _autoStartManager.SetEnabled(_settings.StartWithWindows);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                "自動開始の設定を更新できませんでした。" + Environment.NewLine + ex.Message,
                "Codex Guardian",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }
    }

    private void UpdateTrayMenu()
    {
        if (_notifyIcon.ContextMenuStrip?.Items[0] is ToolStripMenuItem guardToggle)
        {
            guardToggle.Checked = _settings.EnableGuardian;
            guardToggle.Text = _settings.EnableGuardian ? "守護を無効にする" : "守護を有効にする";
        }
    }

    private void UpdateTrayText()
    {
        var text = $"Codex Guardian - {_codexProcessManager.StatusText}";
        _notifyIcon.Text = text.Length <= 63 ? text : text[..63];
        UpdateTrayMenu();
    }

    private void OpenLogDirectory()
    {
        Directory.CreateDirectory(_settingsStore.LogDirectory);
        Process.Start(new ProcessStartInfo
        {
            FileName = _settingsStore.LogDirectory,
            UseShellExecute = true
        });
    }

    private void Notify(string message)
    {
        if (_settings.ShowNotifications)
        {
            _notifyIcon.ShowBalloonTip(2000, "Codex Guardian", message, ToolTipIcon.Info);
        }
    }

    private void ExitApplication()
    {
        _logger.Info("Codex Guardian を終了します。");
        _timer.Stop();
        _watchdogCoordinator.SignalNormalExit();
        _codexProcessManager.StopOnExitIfNeeded();
        _notifyIcon.Visible = false;
        ExitThread();
    }
}
