namespace CodexGuardian.Services;

public static class StartupArguments
{
    public static int GetInt(string[] args, string name, int defaultValue)
    {
        var value = GetValue(args, name);
        return int.TryParse(value, out var parsed) ? parsed : defaultValue;
    }

    public static string? GetValue(string[] args, string name)
    {
        for (var i = 0; i < args.Length; i++)
        {
            if (!string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            return i + 1 < args.Length ? args[i + 1] : null;
        }

        return null;
    }

    public static string Quote(string value)
    {
        return "\"" + value.Replace("\"", "\\\"") + "\"";
    }
}
