using System.Diagnostics;
using System.IO.Pipes;
using System.Text;
using HostEventLauncher.Sharp;

using var view = new StartupConsoleView();
using var shutdownCts = new CancellationTokenSource();
view.OnProgressComplete += () =>
{
    WriteLine("Startup complete, closing console.");
    shutdownCts.Cancel();
};
WriteLine("HostEventLauncher.Runner ready.");

var clientPath = ResolveClientPath(args);
if (clientPath is null)
{
    WriteLine("Client executable not found. Pass the path as the first argument or place it in binaries/client.exe.");
    return 1;
}

var clientDirectory = Path.GetDirectoryName(clientPath)!;
var attachName = $"host-event-launcher-{Environment.ProcessId}-{Guid.NewGuid():N}";
WriteLine($"waiting for attach on '{attachName}'.");

await using var attachChannel = new NamedPipeServerStream(
    attachName,
    PipeDirection.In,
    1,
    PipeTransmissionMode.Byte,
    PipeOptions.Asynchronous);

var startInfo = CreateClientStartInfo(clientPath, clientDirectory, attachName);

WriteLine("starting client...");
using var clientProcess = Process.Start(startInfo);
if (clientProcess is null)
{
    WriteLine("Client process could not be started.");
    return 1;
}

WriteLine("waiting for client attach...");
view.StartSpinner();

try
{
    await attachChannel.WaitForConnectionAsync();
}
catch (Exception ex)
{
    view.StopSpinner();
    WriteLine($"attach failed: {ex.Message}");
    return 1;
}

view.StopSpinner();
WriteLine("client attached.");

using var reader = new StreamReader(attachChannel, Encoding.UTF8, leaveOpen: true);
try
{
    while (true)
    {
        view.StartSpinner();
        var line = await reader.ReadLineAsync(shutdownCts.Token);
        view.StopSpinner();

        if (line is null)
        {
            WriteLine(DescribeDetach(clientProcess));
            break;
        }

        if (!StartupWireMessage.TryParse(line, out var message))
        {
            WriteLine($"unrecognized payload: {line}");
            continue;
        }

        if (message.Kind.Equals("control", StringComparison.OrdinalIgnoreCase) &&
            message.Payload.Equals("close", StringComparison.OrdinalIgnoreCase))
        {
            WriteLine("client closed session.");
            break;
        }

        if (message.Kind.Equals("progress", StringComparison.OrdinalIgnoreCase))
        {
            if (int.TryParse(message.Payload, out var totalSteps) && totalSteps >= 1)
            {
                view.SetProgressTotal(totalSteps);
            }
            else if (message.Payload.Equals("step", StringComparison.OrdinalIgnoreCase))
            {
                view.AdvanceStep();
            }

            continue;
        }

        if (message.Kind.Equals("log", StringComparison.OrdinalIgnoreCase))
        {
            WriteLine(message.Payload);
        }
    }
}
catch (OperationCanceledException)
{
    view.StopSpinner();
    return 0;
}
catch (Exception ex)
{
    view.StopSpinner();
    WriteLine($"error while reading attach channel: {ex.Message}");
    return -1;
}

WriteLine("runner exiting.");
return 0;

void WriteLine(string text) => view.WriteLine(text);

static string DescribeDetach(Process clientProcess)
{
    if (clientProcess.HasExited)
    {
        return $"attach channel closed; client exited with code {clientProcess.ExitCode}.";
    }

    return "attach channel closed; client is still running.";
}

static string? ResolveClientPath(string[] args)
{
    if (args.Length > 0 && File.Exists(args[0]))
    {
        return Path.GetFullPath(args[0]);
    }

    foreach (var directory in new[] { AppContext.BaseDirectory, Environment.CurrentDirectory }.Distinct(StringComparer.OrdinalIgnoreCase))
    {
        var candidate = Path.Combine(directory, "binaries", "client.exe");
        if (File.Exists(candidate))
        {
            return candidate;
        }
    }

    return null;
}

static ProcessStartInfo CreateClientStartInfo(string clientPath, string clientDirectory, string attachName)
{
    return new ProcessStartInfo(clientPath)
    {
        UseShellExecute = false,
        WorkingDirectory = clientDirectory,
        Environment =
        {
            [Startup.RemoteAttachVariable] = attachName
        }
    };
}
