namespace HostEventLauncher.Sharp;

internal static class HostEventLoggerArgsParser
{
    public static HostLoggerLaunchOptions ParseAndRemove(ref string[] args)
    {
        if (args.Length == 0)
        {
            return new HostLoggerLaunchOptions();
        }

        var remaining = new List<string>(args.Length);
        HostLoggerLaunchOptions? parsed = null;

        for (var i = 0; i < args.Length; i++)
        {
            if (!IsSwitch(args[i]))
            {
                remaining.Add(args[i]);
                continue;
            }

            if (i + 1 >= args.Length)
            {
                continue;
            }

            var mode = args[++i];
            if (mode.Equals("console", StringComparison.OrdinalIgnoreCase))
            {
                parsed = new HostLoggerLaunchOptions { Sink = HostLoggerSink.Console };
                continue;
            }

            if (mode.Equals("file", StringComparison.OrdinalIgnoreCase))
            {
                if (i + 1 >= args.Length)
                {
                    continue;
                }

                var filePath = args[++i];
                if (string.IsNullOrWhiteSpace(filePath))
                {
                    continue;
                }

                parsed = new HostLoggerLaunchOptions
                {
                    Sink = HostLoggerSink.File,
                    FilePath = filePath
                };
            }
        }

        args = remaining.ToArray();
        return parsed ?? new HostLoggerLaunchOptions();
    }

    private static bool IsSwitch(string value) =>
        value.Equals(HostLoggerLaunchOptions.SwitchName, StringComparison.OrdinalIgnoreCase);
}
