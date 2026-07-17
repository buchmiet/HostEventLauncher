using System.IO.Pipes;
using System.Text;
using System.Threading.Channels;

namespace HostEventLauncher.Sharp;

public class RemoteStartupSession : IStartupSession
{
    private readonly Lock _sync = new();
    private readonly List<StartupEntry> _entries = [];
    private readonly Channel<StartupWireMessage> _outbox = Channel.CreateUnbounded<StartupWireMessage>(
        new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });
    private readonly CancellationTokenSource _disposeCts = new();
    private readonly Task? _forwarder;
    private int _closed;

    public bool IsEnabled => true;

    public RemoteStartupSession(string? attachName = null)
    {
        attachName = ResolveAttachName(attachName);
        Write("Startup session attached.");

        if (!string.IsNullOrWhiteSpace(attachName))
        {
            _forwarder = Task.Run(() => ForwardAsync(attachName, _disposeCts.Token));
        }
    }

    public virtual void CompleteStep(string? message = null)
    {
        if (!string.IsNullOrWhiteSpace(message))
        {
            Write(message);
        }

        if (Volatile.Read(ref _closed) == 0)
        {
            _outbox.Writer.TryWrite(StartupWireMessage.CompleteStep());
        }
    }

    public void Close()
    {
        if (Interlocked.Exchange(ref _closed, 1) != 0)
        {
            return;
        }

        _outbox.Writer.TryWrite(StartupWireMessage.CreateControl("close"));
        _outbox.Writer.TryComplete();
    }

    public virtual void BeginProgress(int totalSteps)
    {
        if (Volatile.Read(ref _closed) == 0)
        {
            _outbox.Writer.TryWrite(StartupWireMessage.BeginProgress(totalSteps));
        }
    }

    public virtual void Write(string message)
    {
        var entry = new StartupEntry(DateTime.Now, message ?? string.Empty);

        lock (_sync)
        {
            _entries.Add(entry);
        }

        if (Volatile.Read(ref _closed) == 0)
        {
            _outbox.Writer.TryWrite(StartupWireMessage.CreateLog(entry));
        }
    }

    public void Dispose()
    {
        _outbox.Writer.TryComplete();
        _disposeCts.Cancel();

        try
        {
            _forwarder?.Wait(TimeSpan.FromSeconds(1));
        }
        catch
        {
        }

        _disposeCts.Dispose();
    }

    private static string? ResolveAttachName(string? attachName)
    {
        if (!string.IsNullOrWhiteSpace(attachName))
        {
            return attachName;
        }

        return Environment.GetEnvironmentVariable(Startup.RemoteAttachVariable);
    }

    private async Task ForwardAsync(string attachName, CancellationToken cancellationToken)
    {
        try
        {
            await using var channel = new NamedPipeClientStream(
                ".",
                attachName,
                PipeDirection.Out,
                PipeOptions.Asynchronous);

            await channel.ConnectAsync(10_000, cancellationToken).ConfigureAwait(false);

            await using StreamWriter writer = new(channel, new UTF8Encoding(false))
            {
                AutoFlush = true
            };

            await foreach (var message in _outbox.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
            {
                await writer.WriteLineAsync(message.Serialize()).ConfigureAwait(false);
            }
        }
        catch
        {
        }
    }
}
