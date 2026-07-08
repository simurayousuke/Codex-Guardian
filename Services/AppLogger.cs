namespace CodexGuardian.Services;

public sealed class AppLogger
{
    private readonly Lock _sync = new();
    private readonly string _logFilePath;

    public AppLogger(string logFilePath)
    {
        _logFilePath = logFilePath;
        Directory.CreateDirectory(Path.GetDirectoryName(_logFilePath)!);
    }

    public void Info(string message) => Write("INFO", message, null);

    public void Error(string message, Exception? exception = null) => Write("ERROR", message, exception);

    private void Write(string level, string message, Exception? exception)
    {
        try
        {
            lock (_sync)
            {
                RotateIfNeeded();
                var line = $"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff zzz} [{level}] {message}";
                if (exception is not null)
                {
                    line += Environment.NewLine + exception;
                }

                File.AppendAllText(_logFilePath, line + Environment.NewLine);
            }
        }
        catch
        {
            // ログ出力の失敗で守護処理を止めない。
        }
    }

    private void RotateIfNeeded()
    {
        var file = new FileInfo(_logFilePath);
        if (!file.Exists || file.Length < 1024 * 1024)
        {
            return;
        }

        var archive = Path.Combine(file.DirectoryName!, $"guardian-{DateTime.Now:yyyyMMdd-HHmmss}.log");
        File.Move(_logFilePath, archive, overwrite: true);
    }
}
