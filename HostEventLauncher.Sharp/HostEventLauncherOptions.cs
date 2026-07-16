namespace HostEventLauncher.Sharp;

public sealed class HostEventLauncherOptions
{
  public bool AllocateConsole { get; init; } = true;

  public bool CloseOnLastMilestone { get; init; } = true;
}
