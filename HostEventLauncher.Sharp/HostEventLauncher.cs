namespace HostEventLauncher.Sharp;

public static class HostEventLauncher
{
    public const string PipeEnvironmentVariable = "HOST_EVENT_LAUNCHER_PIPE";

    public const string LegacyPipeEnvironmentVariable = "IMPORT_ORDERS_FROM_FILE_RUNNER_PIPE";

    public static IHostEventLog Start(HostEventLauncherOptions? options = null)
    {
        options ??= new HostEventLauncherOptions();

        if (options.AllocateConsole)
        {
            NativeConsole.TryAllocate();
        }

        var console = new HostEventConsole();
        var log = new InProcessHostEventLog(console);

        if (options.CloseOnLastMilestone)
        {
            console.OnLastMilestoneReached += log.SignalShutdown;
        }

        log.Publish("Host event launcher started.");
        return log;
    }

    public static IHostEventLog ConnectPipe(string? pipeName = null) =>
        new PipeHostEventLog(pipeName);
}
