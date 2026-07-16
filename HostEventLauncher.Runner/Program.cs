using System.Diagnostics;
using System.IO.Pipes;
using System.Text;
using HostEventLauncher.Sharp;

using var console = new HostEventConsole();
using var shutdownCts = new CancellationTokenSource();
console.OnLastMilestoneReached += () =>
{
    WriteConsole("Application loaded, shutting down logger");
    shutdownCts.Cancel();
};
WriteConsole("Host event launcher ready.");

var clientPath = ResolveClientPath(args);
if (clientPath is null)
{
    WriteConsole("Client executable not found. Pass the path as the first argument or place it in binaries/.");
    return 1;
}

var clientDirectory = Path.GetDirectoryName(clientPath)!;
var pipeName = $"host-event-launcher-{Environment.ProcessId}-{Guid.NewGuid():N}";
WriteConsole($"opening pipe '{pipeName}'.");

await using var pipeServer = new NamedPipeServerStream(
    pipeName,
    PipeDirection.In,
    1,
    PipeTransmissionMode.Byte,
    PipeOptions.Asynchronous);

var startInfo = CreateClientStartInfo(clientPath, clientDirectory, pipeName);

WriteConsole("starting client...");
using var clientProcess = Process.Start(startInfo);
if (clientProcess is null)
{
    WriteConsole("Client process could not be started.");
    return 1;
}

WriteConsole("waiting for client pipe connection...");
console.StartSpinner();

try
{
    await pipeServer.WaitForConnectionAsync();
}
catch (Exception ex)
{
    console.StopSpinner();
    WriteConsole($"pipe connection failed: {ex.Message}");
    return 1;
}

console.StopSpinner();
WriteConsole("Client connected to pipe.");

using var reader = new StreamReader(pipeServer, Encoding.UTF8, leaveOpen: true);
try
{
    while (true)
    {
        console.StartSpinner();
        var line = await reader.ReadLineAsync(shutdownCts.Token);
        console.StopSpinner();

        if (line is null)
        {
            WriteConsole(DescribePipeClosure(clientProcess));
            break;
        }

        if (!HostEventPipeMessage.TryParse(line, out var message))
        {
            WriteConsole($"unrecognized payload: {line}");
            continue;
        }

        if (message.MessageType.Equals("control", StringComparison.OrdinalIgnoreCase) &&
            message.Text.Equals("kill", StringComparison.OrdinalIgnoreCase))
        {
            WriteConsole("Exiting.");
            break;
        }

        if (message.MessageType.Equals("progress", StringComparison.OrdinalIgnoreCase))
        {
            if (int.TryParse(message.Text, out var total) && total >= 1)
            {
                console.SetMilestoneTotal(total);
            }
            else if (message.Text.Equals("reached", StringComparison.OrdinalIgnoreCase))
            {
                console.AdvanceMilestone();
            }

            continue;
        }

        if (message.MessageType.Equals("log", StringComparison.OrdinalIgnoreCase))
        {
            WriteConsole(message.Text);
        }
    }
}
catch (OperationCanceledException)
{
    console.StopSpinner();
    return 0;
}
catch (Exception ex)
{
    console.StopSpinner();
    WriteConsole($"Error while reading from pipe: {ex.Message}");
    return -1;
}

WriteConsole("runner exiting.");
return 0;

void WriteConsole(string text) => console.WriteMessage(text);

static string DescribePipeClosure(Process clientProcess)
{
    if (clientProcess.HasExited)
    {
        return $"pipe closed; client exited with code {clientProcess.ExitCode}.";
    }

    return "pipe closed; client is still running.";
}

static string? ResolveClientPath(string[] args)
{
    if (args.Length > 0 && File.Exists(args[0]))
    {
        return Path.GetFullPath(args[0]);
    }

    var candidateDirectories = new[]
    {
        AppContext.BaseDirectory,
        Environment.CurrentDirectory
    };

    foreach (var directory in candidateDirectories.Distinct(StringComparer.OrdinalIgnoreCase))
    {
        var candidate = Path.Combine(directory, "binaries", "client.exe");
        if (File.Exists(candidate))
        {
            return candidate;
        }
    }

    return null;
}

static ProcessStartInfo CreateClientStartInfo(string clientPath, string clientDirectory, string pipeName)
{
    return new ProcessStartInfo(clientPath)
    {
        UseShellExecute = false,
        WorkingDirectory = clientDirectory,
        Environment =
        {
            [global::HostEventLauncher.Sharp.HostEventLauncher.PipeEnvironmentVariable] = pipeName,
            [global::HostEventLauncher.Sharp.HostEventLauncher.LegacyPipeEnvironmentVariable] = pipeName
        }
    };
}
