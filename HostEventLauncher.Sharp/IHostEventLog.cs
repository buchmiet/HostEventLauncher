namespace HostEventLauncher.Sharp;

public interface IHostEventLog : IDisposable
{
    void Publish(string text);
    void MilestoneReached(string text);
    void SetNumberOfMilestones(int number);
    void SignalShutdown();
}
