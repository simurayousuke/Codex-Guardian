using CodexGuardian.Services;
using CodexGuardian.UI;

namespace CodexGuardian;

internal static class Program
{
    private const string SingleInstanceMutexName = @"Local\CodexGuardian.SingleInstance";

    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Any(arg => string.Equals(arg, "--watchdog", StringComparison.OrdinalIgnoreCase)))
        {
            return WatchdogCoordinator.RunWatchdog(args);
        }

        using var mutex = new Mutex(true, SingleInstanceMutexName, out var createdNew);
        if (!createdNew)
        {
            MessageBox.Show(
                "Codex Guardian は既に起動しています。",
                "Codex Guardian",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return 0;
        }

        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        var store = new SettingsStore();
        var settings = store.Load();
        var recoveredCount = StartupArguments.GetInt(args, "--recovered-count", 0);

        using var context = new GuardianApplicationContext(store, settings, recoveredCount);
        Application.Run(context);
        return 0;
    }
}
