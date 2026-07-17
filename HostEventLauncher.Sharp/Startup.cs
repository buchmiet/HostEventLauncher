namespace HostEventLauncher.Sharp;

/// <summary>
/// Static entry point for opening or attaching a startup reporting session.
/// </summary>
public static class Startup
{
    /// <summary>
    /// Environment variable set by <c>HostEventLauncher.Runner</c> when the app is spawned with a detached console.
    /// </summary>
    public const string RemoteAttachVariable = "HOST_EVENT_LAUNCHER_ATTACH";

    /// <summary>
    /// Parses <c>--HostEventLogger console|file &lt;path&gt;</c>, removes those tokens from <paramref name="args"/>,
    /// and opens the configured startup session.
    /// </summary>
    public static IStartupSession Open(ref string[] args)
    {
        var launch = HostEventLoggerArgsParser.ParseAndRemove(ref args);
        return Open(launch);
    }

    public static IStartupSession Open(HostLoggerLaunchOptions options) =>
        options.Sink switch
        {
            HostLoggerSink.Console => OpenConsole(options),
            HostLoggerSink.File => OpenFile(options),
            _ => NullStartupSession.Instance
        };

    /// <summary>
    /// Attaches to a startup console owned by another process (typically <c>HostEventLauncher.Runner</c>).
    /// </summary>
    public static IStartupSession Attach(string? attachName = null) =>
        new RemoteStartupSession(attachName);

    private static IStartupSession OpenConsole(HostLoggerLaunchOptions options)
    {
        ConsoleHost.EnsureAttached();

        var view = new StartupConsoleView();
        var session = new LocalStartupSession(view);

        if (options.CloseWhenComplete)
        {
            view.OnProgressComplete += session.Close;
        }

        session.Write("Startup console opened.");
        return session;
    }

    private static IStartupSession OpenFile(HostLoggerLaunchOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.FilePath))
        {
            throw new ArgumentException("File path is required when sink is File.", nameof(options));
        }

        return new FileStartupSession(options.FilePath);
    }
}
