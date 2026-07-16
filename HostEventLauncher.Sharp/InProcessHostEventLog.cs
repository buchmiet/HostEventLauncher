namespace HostEventLauncher.Sharp;

public sealed class InProcessHostEventLog : IHostEventLog
{
    private readonly HostEventConsole _console;
    private readonly bool _ownsConsole;
    private int _shutdownRequested;

    public InProcessHostEventLog(HostEventConsole console, bool ownsConsole = true)
    {
        _console = console;
        _ownsConsole = ownsConsole;
    }

    public void Publish(string text)
    {
        _console.WriteMessage(text ?? string.Empty);
    }

    public void MilestoneReached(string text)
    {
        Publish(text);
        if (Volatile.Read(ref _shutdownRequested) == 0)
        {
            _console.AdvanceMilestone();
        }
    }

    public void SetNumberOfMilestones(int number)
    {
        if (Volatile.Read(ref _shutdownRequested) == 0)
        {
            _console.SetMilestoneTotal(number);
        }
    }

    public void SignalShutdown()
    {
        Interlocked.Exchange(ref _shutdownRequested, 1);
    }

    public void Dispose()
    {
        SignalShutdown();
        if (_ownsConsole)
        {
            _console.Dispose();
        }
    }
}
