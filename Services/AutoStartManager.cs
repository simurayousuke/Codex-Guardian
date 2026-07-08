using Microsoft.Win32;

namespace CodexGuardian.Services;

public sealed class AutoStartManager
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "Codex Guardian";
    private readonly AppLogger _logger;

    public AutoStartManager(AppLogger logger)
    {
        _logger = logger;
    }

    public bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
        var value = key?.GetValue(ValueName) as string;
        return !string.IsNullOrWhiteSpace(value);
    }

    public void SetEnabled(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true)
                ?? Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);

            if (enabled)
            {
                key.SetValue(ValueName, $"{StartupArguments.Quote(Application.ExecutablePath)} --from-autostart");
                _logger.Info("Windows 起動時の自動開始を有効にしました。");
            }
            else
            {
                key.DeleteValue(ValueName, throwOnMissingValue: false);
                _logger.Info("Windows 起動時の自動開始を無効にしました。");
            }
        }
        catch (Exception ex)
        {
            _logger.Error("自動開始設定の更新に失敗しました。", ex);
            throw;
        }
    }
}
