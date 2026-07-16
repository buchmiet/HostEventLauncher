using System.IO.Pipes;
using System.Text;
using System.Threading.Channels;

namespace HostEventLauncher.Sharp;

public class PipeHostEventLog : IHostEventLog
{
    private readonly Lock _sync = new();
    private readonly List<HostEventEntry> _entries = [];
    private readonly Channel<HostEventPipeMessage> _messages = Channel.CreateUnbounded<HostEventPipeMessage>(
        new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });
    private readonly CancellationTokenSource _disposeCts = new();
    private readonly Task? _pumpTask;
    private int _shutdownRequested;

    public PipeHostEventLog(string? pipeName = null)
    {
        pipeName = ResolvePipeName(pipeName);
        Publish("Host event launcher initialized.");

        if (!string.IsNullOrWhiteSpace(pipeName))
        {
            _pumpTask = Task.Run(() => PumpAsync(pipeName, _disposeCts.Token));
        }
    }

    public virtual void MilestoneReached(string text)
    {
        Publish(text);
        if (Volatile.Read(ref _shutdownRequested) == 0)
        {
            _messages.Writer.TryWrite(HostEventPipeMessage.ReachMilestone());
        }
    }

    public void SignalShutdown()
    {
        if (Interlocked.Exchange(ref _shutdownRequested, 1) != 0)
        {
            return;
        }

        _messages.Writer.TryWrite(HostEventPipeMessage.CreateControl("kill"));
        _messages.Writer.TryComplete();
    }

    public virtual void SetNumberOfMilestones(int number)
    {
        if (Volatile.Read(ref _shutdownRequested) == 0)
        {
            _messages.Writer.TryWrite(HostEventPipeMessage.SetNumberOfMilestones(number));
        }
    }

    public virtual void Publish(string text)
    {
        var entry = new HostEventEntry(DateTime.Now, text ?? string.Empty);

        lock (_sync)
        {
            _entries.Add(entry);
        }

        if (Volatile.Read(ref _shutdownRequested) == 0)
        {
            _messages.Writer.TryWrite(HostEventPipeMessage.CreateLog(entry));
        }
    }

    public void Dispose()
    {
        _messages.Writer.TryComplete();
        _disposeCts.Cancel();

        try
        {
            _pumpTask?.Wait(TimeSpan.FromSeconds(1));
        }
        catch
        {
        }

        _disposeCts.Dispose();
    }

    private static string? ResolvePipeName(string? pipeName)
    {
        if (!string.IsNullOrWhiteSpace(pipeName))
        {
            return pipeName;
        }

        return Environment.GetEnvironmentVariable(HostEventLauncher.PipeEnvironmentVariable)
            ?? Environment.GetEnvironmentVariable(HostEventLauncher.LegacyPipeEnvironmentVariable);
    }

    private async Task PumpAsync(string pipeName, CancellationToken cancellationToken)
    {
        try
        {
            await using var pipe = new NamedPipeClientStream(
                ".",
                pipeName,
                PipeDirection.Out,
                PipeOptions.Asynchronous);

            await pipe.ConnectAsync(10_000, cancellationToken).ConfigureAwait(false);

            await using StreamWriter writer = new(pipe, new UTF8Encoding(false))
            {
                AutoFlush = true
            };

            await foreach (var message in _messages.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
            {
                await writer.WriteLineAsync(message.Serialize()).ConfigureAwait(false);
            }
        }
        catch
        {
        }
    }
}
