namespace CodexGuardian.Models;

public sealed class AppSettings
{
    public bool EnableGuardian { get; set; } = true;
    public bool StartWithWindows { get; set; } = true;
    public bool StartCodexOnGuardianLaunch { get; set; } = true;
    public string CodexExecutablePath { get; set; } = "codex";
    public string CodexArguments { get; set; } = string.Empty;
    public string WorkingDirectory { get; set; } = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    public int RestartDelaySeconds { get; set; } = 5;
    public int MaxRestartAttempts { get; set; } = 5;
    public int FailureCooldownSeconds { get; set; } = 60;
    public bool ShowNotifications { get; set; } = true;
    public bool LaunchInNewWindow { get; set; } = true;
    public bool RestartExternalCodexProcesses { get; set; }
    public bool KillCodexOnExit { get; set; }
    public bool EnableSelfRecovery { get; set; }
    public int SelfRecoveryDelaySeconds { get; set; } = 3;
    public int SelfRecoveryMaxRestarts { get; set; } = 3;
    public int SelfRecoveryResetAfterSeconds { get; set; } = 300;

    public void Normalize()
    {
        CodexExecutablePath = string.IsNullOrWhiteSpace(CodexExecutablePath) ? "codex" : CodexExecutablePath.Trim();
        CodexArguments = CodexArguments.Trim();
        WorkingDirectory = string.IsNullOrWhiteSpace(WorkingDirectory)
            ? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
            : WorkingDirectory.Trim();

        RestartDelaySeconds = Math.Clamp(RestartDelaySeconds, 0, 3600);
        MaxRestartAttempts = Math.Clamp(MaxRestartAttempts, 1, 100);
        FailureCooldownSeconds = Math.Clamp(FailureCooldownSeconds, 1, 86400);
        SelfRecoveryDelaySeconds = Math.Clamp(SelfRecoveryDelaySeconds, 1, 3600);
        SelfRecoveryMaxRestarts = Math.Clamp(SelfRecoveryMaxRestarts, 1, 100);
        SelfRecoveryResetAfterSeconds = Math.Clamp(SelfRecoveryResetAfterSeconds, 10, 86400);
    }
}
