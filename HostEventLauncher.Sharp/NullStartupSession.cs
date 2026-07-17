namespace HostEventLauncher.Sharp;

public sealed class NullStartupSession : IStartupSession
{
    public static NullStartupSession Instance { get; } = new();

    public bool IsEnabled => false;

    public void Write(string message)
    {
    }

    public void BeginProgress(int totalSteps)
    {
    }

    public void CompleteStep(string? message = null)
    {
    }

    public void Close()
    {
    }

    public void Dispose()
    {
    }
}
