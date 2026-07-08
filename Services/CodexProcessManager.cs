using System.ComponentModel;
using System.Diagnostics;
using CodexGuardian.Models;

namespace CodexGuardian.Services;

public sealed class CodexProcessManager
{
    private readonly AppLogger _logger;
    private AppSettings _settings;
    private Process? _managedProcess;
    private bool _lastKnownRunning;
    private DateTimeOffset? _nextRestartAt;
    private DateTimeOffset? _cooldownUntil;
    private int _consecutiveFailures;

    public event EventHandler? StateChanged;

    public CodexProcessManager(AppSettings settings, AppLogger logger)
    {
        _settings = settings;
        _logger = logger;
    }

    public bool IsRunning { get; private set; }
    public string StatusText { get; private set; } = "待機中";

    public void ApplySettings(AppSettings settings)
    {
        _settings = settings;
        _settings.Normalize();
        UpdateState();
    }

    public void StartInitialIfNeeded()
    {
        UpdateState();
        if (_settings.EnableGuardian && _settings.StartCodexOnGuardianLaunch && !IsRunning)
        {
            StartCodex("起動時チェック");
        }
    }

    public void Tick()
    {
        UpdateState();

        if (!_settings.EnableGuardian)
        {
            _nextRestartAt = null;
            SetStatus(IsRunning ? "Codex 実行中 / 守護停止中" : "守護停止中");
            return;
        }

        if (IsRunning)
        {
            _nextRestartAt = null;
            _cooldownUntil = null;
            _consecutiveFailures = 0;
            SetStatus("Codex 実行中");
            _lastKnownRunning = true;
            return;
        }

        if (_lastKnownRunning)
        {
            _logger.Info("Codex の終了を検出しました。");
            _lastKnownRunning = false;
        }

        var now = DateTimeOffset.Now;
        if (_cooldownUntil is { } cooldown && now < cooldown)
        {
            SetStatus($"再起動待機中 ({(int)Math.Ceiling((cooldown - now).TotalSeconds)} 秒)");
            return;
        }

        _nextRestartAt ??= now.AddSeconds(_settings.RestartDelaySeconds);
        if (now < _nextRestartAt.Value)
        {
            SetStatus($"再起動予定 ({(int)Math.Ceiling((_nextRestartAt.Value - now).TotalSeconds)} 秒)");
            return;
        }

        StartCodex("守護再起動");
    }

    public void StartCodex(string reason)
    {
        try
        {
            UpdateState();
            if (IsRunning)
            {
                SetStatus("Codex 実行中");
                return;
            }

            _logger.Info($"Codex を起動します。理由: {reason}");
            var startInfo = CreateStartInfo();
            _managedProcess = Process.Start(startInfo);
            _nextRestartAt = null;
            _cooldownUntil = null;
            _consecutiveFailures = 0;
            SetStatus("Codex 起動済み");
        }
        catch (Exception ex)
        {
            _consecutiveFailures++;
            _nextRestartAt = null;
            _logger.Error("Codex の起動に失敗しました。", ex);

            if (_consecutiveFailures >= _settings.MaxRestartAttempts)
            {
                _cooldownUntil = DateTimeOffset.Now.AddSeconds(_settings.FailureCooldownSeconds);
                _consecutiveFailures = 0;
                SetStatus("起動失敗 / 冷却中");
            }
            else
            {
                _nextRestartAt = DateTimeOffset.Now.AddSeconds(_settings.RestartDelaySeconds);
                SetStatus("起動失敗 / 再試行待ち");
            }
        }
    }

    public void RestartCodex()
    {
        _logger.Info("Codex の手動再起動を開始します。");
        StopCodexForRestart();
        Thread.Sleep(500);
        StartCodex("手動再起動");
    }

    public void StopOnExitIfNeeded()
    {
        if (!_settings.KillCodexOnExit)
        {
            return;
        }

        StopCodexForRestart();
    }

    private void StopCodexForRestart()
    {
        var targets = new List<Process>();

        if (_managedProcess is { HasExited: false })
        {
            targets.Add(_managedProcess);
        }

        if (_settings.RestartExternalCodexProcesses)
        {
            foreach (var process in FindMatchingProcesses())
            {
                if (targets.All(existing => existing.Id != process.Id))
                {
                    targets.Add(process);
                }
                else
                {
                    process.Dispose();
                }
            }
        }

        foreach (var process in targets)
        {
            TryTerminate(process);
        }

        _managedProcess = null;
        _lastKnownRunning = false;
        UpdateState();
    }

    private ProcessStartInfo CreateStartInfo()
    {
        var executable = _settings.CodexExecutablePath;
        var workingDirectory = Directory.Exists(_settings.WorkingDirectory)
            ? _settings.WorkingDirectory
            : Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        var startInfo = new ProcessStartInfo
        {
            FileName = executable,
            Arguments = _settings.CodexArguments,
            WorkingDirectory = workingDirectory,
            UseShellExecute = _settings.LaunchInNewWindow,
            CreateNoWindow = !_settings.LaunchInNewWindow
        };

        if (!_settings.LaunchInNewWindow)
        {
            startInfo.WindowStyle = ProcessWindowStyle.Hidden;
        }

        return startInfo;
    }

    private void UpdateState()
    {
        var running = IsManagedProcessRunning() || HasMatchingProcess();
        IsRunning = running;
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    private bool HasMatchingProcess()
    {
        foreach (var process in FindMatchingProcesses())
        {
            process.Dispose();
            return true;
        }

        return false;
    }

    private bool IsManagedProcessRunning()
    {
        try
        {
            return _managedProcess is { HasExited: false };
        }
        catch
        {
            return false;
        }
    }

    private IEnumerable<Process> FindMatchingProcesses()
    {
        var processName = GetExpectedProcessName();
        if (string.IsNullOrWhiteSpace(processName))
        {
            yield break;
        }

        Process[] processes;
        try
        {
            processes = Process.GetProcessesByName(processName);
        }
        catch
        {
            yield break;
        }

        var expectedPath = GetExpectedFullPath();
        foreach (var process in processes)
        {
            if (expectedPath is null)
            {
                yield return process;
                continue;
            }

            string? actualPath = null;
            try
            {
                actualPath = process.MainModule?.FileName;
            }
            catch (Win32Exception)
            {
            }
            catch (InvalidOperationException)
            {
            }

            if (string.Equals(actualPath, expectedPath, StringComparison.OrdinalIgnoreCase))
            {
                yield return process;
            }
            else
            {
                process.Dispose();
            }
        }
    }

    private string GetExpectedProcessName()
    {
        var fileName = Path.GetFileNameWithoutExtension(_settings.CodexExecutablePath);
        return string.IsNullOrWhiteSpace(fileName) ? "codex" : fileName;
    }

    private string? GetExpectedFullPath()
    {
        try
        {
            if (Path.IsPathFullyQualified(_settings.CodexExecutablePath) && File.Exists(_settings.CodexExecutablePath))
            {
                return Path.GetFullPath(_settings.CodexExecutablePath);
            }
        }
        catch
        {
        }

        return null;
    }

    private void TryTerminate(Process process)
    {
        try
        {
            _logger.Info($"プロセスを終了します。PID={process.Id}, Name={process.ProcessName}");
            if (process.CloseMainWindow() && process.WaitForExit(3000))
            {
                return;
            }

            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit(5000);
            }
        }
        catch (Exception ex)
        {
            _logger.Error($"プロセス終了に失敗しました。PID={SafeGetId(process)}", ex);
        }
        finally
        {
            process.Dispose();
        }
    }

    private static int SafeGetId(Process process)
    {
        try
        {
            return process.Id;
        }
        catch
        {
            return -1;
        }
    }

    private void SetStatus(string status)
    {
        if (StatusText == status)
        {
            return;
        }

        StatusText = status;
        StateChanged?.Invoke(this, EventArgs.Empty);
    }
}
