using CodexGuardian.Models;

namespace CodexGuardian.UI;

public sealed class SettingsForm : Form
{
    private readonly CheckBox _enableGuardian = new() { Text = "Codex の守護を有効にする", AutoSize = true };
    private readonly CheckBox _startWithWindows = new() { Text = "Windows 起動時に開始する", AutoSize = true };
    private readonly CheckBox _startCodexOnLaunch = new() { Text = "起動時に Codex を開始する", AutoSize = true };
    private readonly CheckBox _showNotifications = new() { Text = "通知を表示する", AutoSize = true };
    private readonly CheckBox _launchInNewWindow = new() { Text = "Codex を新しいウィンドウで起動する", AutoSize = true };
    private readonly CheckBox _restartExternal = new() { Text = "管理外の Codex も再起動対象にする", AutoSize = true };
    private readonly CheckBox _killCodexOnExit = new() { Text = "終了時に Codex も終了する", AutoSize = true };
    private readonly CheckBox _enableSelfRecovery = new() { Text = "守護ツール自身を自動復旧する", AutoSize = true };
    private readonly TextBox _codexPath = new() { Anchor = AnchorStyles.Left | AnchorStyles.Right };
    private readonly TextBox _arguments = new() { Anchor = AnchorStyles.Left | AnchorStyles.Right };
    private readonly TextBox _workingDirectory = new() { Anchor = AnchorStyles.Left | AnchorStyles.Right };
    private readonly NumericUpDown _restartDelay = CreateNumeric(0, 3600);
    private readonly NumericUpDown _maxAttempts = CreateNumeric(1, 100);
    private readonly NumericUpDown _cooldown = CreateNumeric(1, 86400);
    private readonly NumericUpDown _selfRecoveryDelay = CreateNumeric(1, 3600);
    private readonly NumericUpDown _selfRecoveryMax = CreateNumeric(1, 100);
    private readonly NumericUpDown _selfRecoveryReset = CreateNumeric(10, 86400);

    public event EventHandler<AppSettings>? SettingsSaved;

    public SettingsForm(AppSettings settings)
    {
        Text = "Codex Guardian 設定";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(640, 640);
        Size = new Size(760, 720);
        AutoScaleMode = AutoScaleMode.Dpi;

        BuildLayout();
        LoadSettings(settings);
    }

    private void BuildLayout()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(16)
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var title = new Label
        {
            Text = "Codex Guardian",
            Font = new Font(Font.FontFamily, 14, FontStyle.Bold),
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 12)
        };

        var content = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            AutoScroll = true
        };
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 220));
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 92));

        AddFullRow(content, _enableGuardian);
        AddFullRow(content, _startWithWindows);
        AddFullRow(content, _startCodexOnLaunch);
        AddFullRow(content, _showNotifications);
        AddFullRow(content, _launchInNewWindow);
        AddFullRow(content, _restartExternal);
        AddFullRow(content, _killCodexOnExit);
        AddPathRow(content, "Codex のパス", _codexPath, "参照...", BrowseExecutable);
        AddRow(content, "起動引数", _arguments);
        AddPathRow(content, "作業フォルダー", _workingDirectory, "参照...", BrowseWorkingDirectory);
        AddRow(content, "再起動までの秒数", _restartDelay);
        AddRow(content, "最大連続再試行回数", _maxAttempts);
        AddRow(content, "失敗後の冷却秒数", _cooldown);
        AddFullRow(content, new Label
        {
            Text = "守護ツール自身の復旧",
            Font = new Font(Font.FontFamily, 10, FontStyle.Bold),
            AutoSize = true,
            Margin = new Padding(0, 18, 0, 4)
        });
        AddFullRow(content, _enableSelfRecovery);
        AddRow(content, "復旧までの秒数", _selfRecoveryDelay);
        AddRow(content, "最大連続復旧回数", _selfRecoveryMax);
        AddRow(content, "連続回数を戻す秒数", _selfRecoveryReset);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true
        };
        var save = new Button { Text = "保存", AutoSize = true, DialogResult = DialogResult.OK };
        var cancel = new Button { Text = "キャンセル", AutoSize = true, DialogResult = DialogResult.Cancel };
        save.Click += (_, _) => Save();
        cancel.Click += (_, _) => Close();
        buttons.Controls.Add(save);
        buttons.Controls.Add(cancel);

        root.Controls.Add(title, 0, 0);
        root.Controls.Add(content, 0, 1);
        root.Controls.Add(buttons, 0, 2);
        Controls.Add(root);
    }

    private void LoadSettings(AppSettings settings)
    {
        _enableGuardian.Checked = settings.EnableGuardian;
        _startWithWindows.Checked = settings.StartWithWindows;
        _startCodexOnLaunch.Checked = settings.StartCodexOnGuardianLaunch;
        _showNotifications.Checked = settings.ShowNotifications;
        _launchInNewWindow.Checked = settings.LaunchInNewWindow;
        _restartExternal.Checked = settings.RestartExternalCodexProcesses;
        _killCodexOnExit.Checked = settings.KillCodexOnExit;
        _enableSelfRecovery.Checked = settings.EnableSelfRecovery;
        _codexPath.Text = settings.CodexExecutablePath;
        _arguments.Text = settings.CodexArguments;
        _workingDirectory.Text = settings.WorkingDirectory;
        _restartDelay.Value = settings.RestartDelaySeconds;
        _maxAttempts.Value = settings.MaxRestartAttempts;
        _cooldown.Value = settings.FailureCooldownSeconds;
        _selfRecoveryDelay.Value = settings.SelfRecoveryDelaySeconds;
        _selfRecoveryMax.Value = settings.SelfRecoveryMaxRestarts;
        _selfRecoveryReset.Value = settings.SelfRecoveryResetAfterSeconds;
    }

    private void Save()
    {
        var settings = new AppSettings
        {
            EnableGuardian = _enableGuardian.Checked,
            StartWithWindows = _startWithWindows.Checked,
            StartCodexOnGuardianLaunch = _startCodexOnLaunch.Checked,
            ShowNotifications = _showNotifications.Checked,
            LaunchInNewWindow = _launchInNewWindow.Checked,
            RestartExternalCodexProcesses = _restartExternal.Checked,
            KillCodexOnExit = _killCodexOnExit.Checked,
            EnableSelfRecovery = _enableSelfRecovery.Checked,
            CodexExecutablePath = _codexPath.Text,
            CodexArguments = _arguments.Text,
            WorkingDirectory = _workingDirectory.Text,
            RestartDelaySeconds = (int)_restartDelay.Value,
            MaxRestartAttempts = (int)_maxAttempts.Value,
            FailureCooldownSeconds = (int)_cooldown.Value,
            SelfRecoveryDelaySeconds = (int)_selfRecoveryDelay.Value,
            SelfRecoveryMaxRestarts = (int)_selfRecoveryMax.Value,
            SelfRecoveryResetAfterSeconds = (int)_selfRecoveryReset.Value
        };

        settings.Normalize();
        SettingsSaved?.Invoke(this, settings);
        Close();
    }

    private void BrowseExecutable()
    {
        using var dialog = new OpenFileDialog
        {
            Title = "Codex の実行ファイルを選択",
            Filter = "実行ファイル|*.exe;*.cmd;*.bat|すべてのファイル|*.*",
            CheckFileExists = true
        };

        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            _codexPath.Text = dialog.FileName;
        }
    }

    private void BrowseWorkingDirectory()
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "作業フォルダーを選択",
            UseDescriptionForTitle = true
        };

        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            _workingDirectory.Text = dialog.SelectedPath;
        }
    }

    private static NumericUpDown CreateNumeric(int min, int max)
    {
        return new NumericUpDown
        {
            Minimum = min,
            Maximum = max,
            Anchor = AnchorStyles.Left,
            Width = 120
        };
    }

    private static void AddFullRow(TableLayoutPanel table, Control control)
    {
        var row = table.RowCount++;
        table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        control.Margin = new Padding(0, 4, 0, 4);
        table.Controls.Add(control, 0, row);
        table.SetColumnSpan(control, 3);
    }

    private static void AddRow(TableLayoutPanel table, string labelText, Control input)
    {
        var row = table.RowCount++;
        table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        var label = new Label
        {
            Text = labelText,
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(0, 8, 8, 8)
        };
        input.Margin = new Padding(0, 4, 8, 4);
        table.Controls.Add(label, 0, row);
        table.Controls.Add(input, 1, row);
        table.SetColumnSpan(input, 2);
    }

    private static void AddPathRow(TableLayoutPanel table, string labelText, TextBox textBox, string buttonText, Action browse)
    {
        var row = table.RowCount++;
        table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        var label = new Label
        {
            Text = labelText,
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(0, 8, 8, 8)
        };
        var button = new Button
        {
            Text = buttonText,
            AutoSize = true,
            Anchor = AnchorStyles.Left
        };
        button.Click += (_, _) => browse();
        textBox.Margin = new Padding(0, 4, 8, 4);
        button.Margin = new Padding(0, 4, 0, 4);
        table.Controls.Add(label, 0, row);
        table.Controls.Add(textBox, 1, row);
        table.Controls.Add(button, 2, row);
    }
}
