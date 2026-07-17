using System.Text;

namespace HostEventLauncher.Sharp;

public sealed class FileStartupSession : IStartupSession
{
    private readonly StreamWriter _writer;
    private readonly Lock _sync = new();
    private int _closed;
    private int _totalSteps;
    private int _completedSteps;

    public FileStartupSession(string filePath)
    {
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        _writer = new StreamWriter(filePath, append: false, new UTF8Encoding(false))
        {
            AutoFlush = true
        };
        Write($"Host event logger file: {filePath}");
    }

    public bool IsEnabled => true;

    public void Write(string message)
    {
        if (Volatile.Read(ref _closed) != 0)
        {
            return;
        }

        lock (_sync)
        {
            _writer.WriteLine($"[{DateTime.Now:HH:mm:ss.ffffff}] {message ?? string.Empty}");
        }
    }

    public void BeginProgress(int totalSteps)
    {
        if (Volatile.Read(ref _closed) != 0)
        {
            return;
        }

        _totalSteps = totalSteps;
        _completedSteps = 0;
        Write($"[progress] begin {totalSteps} steps");
    }

    public void CompleteStep(string? message = null)
    {
        if (!string.IsNullOrWhiteSpace(message))
        {
            Write(message);
        }

        if (Volatile.Read(ref _closed) != 0)
        {
            return;
        }

        _completedSteps++;
        if (_totalSteps > 0)
        {
            Write($"[progress] {_completedSteps}/{_totalSteps}");
        }
    }

    public void Close() => Interlocked.Exchange(ref _closed, 1);

    public void Dispose()
    {
        Close();
        _writer.Dispose();
    }
}
