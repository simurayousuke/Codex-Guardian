using System.Diagnostics;
using CodexGuardian.Models;

namespace CodexGuardian.Services;

public sealed class WatchdogCoordinator : IDisposable
{
    private AppSettings _settings;
    private readonly AppLogger _logger;
    private readonly int _recoveredCount;
    private readonly string _exitEventName;
    private EventWaitHandle? _exitEvent;
    private Process? _watchdogProcess;

    public WatchdogCoordinator(AppSettings settings, AppLogger logger, int recoveredCount)
    {
        _settings = settings;
        _logger = logger;
        _recoveredCount = recoveredCount;
        _exitEventName = $@"Local\CodexGuardian.Exit.{Guid.NewGuid():N}";
    }

    public void ApplySettings(AppSettings settings)
    {
        _settings = settings;
        if (settings.EnableSelfRecovery && (_watchdogProcess is null || _watchdogProcess.HasExited))
        {
            Start();
        }
    }

    public void Start()
    {
        if (!_settings.EnableSelfRecovery || _watchdogProcess is { HasExited: false })
        {
            return;
        }

        try
        {
            _exitEvent = new EventWaitHandle(false, EventResetMode.ManualReset, _exitEventName);
            var args = string.Join(
                " ",
                "--watchdog",
                "--parent-pid",
                Environment.ProcessId.ToString(),
                "--exit-event",
                StartupArguments.Quote(_exitEventName),
                "--recovery-count",
                _recoveredCount.ToString());

            _watchdogProcess = Process.Start(new ProcessStartInfo
            {
                FileName = Application.ExecutablePath,
                Arguments = args,
                UseShellExecute = false,
                CreateNoWindow = true
            });

            _logger.Info("守護ツール自身の自動復旧を開始しました。");
        }
        catch (Exception ex)
        {
            _logger.Error("守護ツール自身の自動復旧を開始できませんでした。", ex);
        }
    }

    public void SignalNormalExit()
    {
        try
        {
            _exitEvent?.Set();
        }
        catch
        {
        }
    }

    public void Dispose()
    {
        SignalNormalExit();
        _exitEvent?.Dispose();
        _watchdogProcess?.Dispose();
    }

    public static int RunWatchdog(string[] args)
    {
        var store = new SettingsStore();
        var logger = new AppLogger(store.LogFilePath);
        try
        {
            var parentPid = StartupArguments.GetInt(args, "--parent-pid", -1);
            var exitEventName = StartupArguments.GetValue(args, "--exit-event");
            var recoveryCount = StartupArguments.GetInt(args, "--recovery-count", 0);

            if (parentPid <= 0 || string.IsNullOrWhiteSpace(exitEventName))
            {
                logger.Error("自動復旧プロセスの引数が不正です。");
                return 2;
            }

            var settings = store.Load();
            settings.Normalize();
            if (!settings.EnableSelfRecovery)
            {
                logger.Info("自動復旧が無効なため、監視を終了します。");
                return 0;
            }

            using var exitEvent = EventWaitHandle.OpenExisting(exitEventName);
            using var parent = Process.GetProcessById(parentPid);
            var startTime = SafeGetStartTime(parent);

            while (true)
            {
                if (exitEvent.WaitOne(1000))
                {
                    logger.Info("通常終了を検出したため、自動復旧を行いません。");
                    return 0;
                }

                parent.Refresh();
                if (parent.HasExited)
                {
                    break;
                }
            }

            settings = store.Load();
            settings.Normalize();
            if (!settings.EnableSelfRecovery)
            {
                logger.Info("終了検出時点で自動復旧が無効なため、再起動しません。");
                return 0;
            }

            var runtime = startTime is null ? TimeSpan.Zero : DateTime.Now - startTime.Value;
            var nextRecoveryCount = runtime.TotalSeconds >= settings.SelfRecoveryResetAfterSeconds
                ? 1
                : recoveryCount + 1;

            if (nextRecoveryCount > settings.SelfRecoveryMaxRestarts)
            {
                logger.Error("守護ツール自身の連続復旧回数が上限に達したため、再起動しません。");
                return 3;
            }

            logger.Info($"守護ツール自身の終了を検出しました。{settings.SelfRecoveryDelaySeconds} 秒後に再起動します。");
            if (exitEvent.WaitOne(TimeSpan.FromSeconds(settings.SelfRecoveryDelaySeconds)))
            {
                logger.Info("再起動待機中に通常終了シグナルを検出しました。");
                return 0;
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = Application.ExecutablePath,
                Arguments = $"--recovered-count {nextRecoveryCount}",
                UseShellExecute = true
            });

            logger.Info("守護ツール自身を再起動しました。");
            return 0;
        }
        catch (Exception ex)
        {
            logger.Error("自動復旧プロセスでエラーが発生しました。", ex);
            return 1;
        }
    }

    private static DateTime? SafeGetStartTime(Process process)
    {
        try
        {
            return process.StartTime;
        }
        catch
        {
            return null;
        }
    }
}
