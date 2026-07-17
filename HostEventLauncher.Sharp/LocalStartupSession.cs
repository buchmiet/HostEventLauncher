namespace HostEventLauncher.Sharp;

public sealed class LocalStartupSession : IStartupSession
{
    private readonly StartupConsoleView _view;
    private readonly bool _ownsView;
    private int _closed;

    internal LocalStartupSession(StartupConsoleView view, bool ownsView = true)
    {
        _view = view;
        _ownsView = ownsView;
    }

    public bool IsEnabled => true;

    public void Write(string message) => _view.WriteLine(message ?? string.Empty);

    public void CompleteStep(string? message = null)
    {
        if (!string.IsNullOrWhiteSpace(message))
        {
            Write(message);
        }

        if (Volatile.Read(ref _closed) == 0)
        {
            _view.AdvanceStep();
        }
    }

    public void BeginProgress(int totalSteps)
    {
        if (Volatile.Read(ref _closed) == 0)
        {
            _view.SetProgressTotal(totalSteps);
        }
    }

    public void Close() => Interlocked.Exchange(ref _closed, 1);

    public void Dispose()
    {
        Close();
        if (_ownsView)
        {
            _view.Dispose();
        }
    }
}
