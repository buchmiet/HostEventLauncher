namespace HostEventLauncher.Sharp;

public sealed class HostLoggerLaunchOptions
{
    public const string SwitchName = "--HostEventLogger";

    public HostLoggerSink Sink { get; init; } = HostLoggerSink.None;

    public string? FilePath { get; init; }

    public bool CloseWhenComplete { get; init; } = true;
}
