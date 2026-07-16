# HostEventLauncher

Startup event logging for .NET host applications — console progress, milestones, and optional named-pipe bridge to an external runner.

## HostEventLauncher.Sharp

Call early in `Main` to allocate a console and publish startup events in-process:

```csharp
using HostEventLauncher.Sharp;

var log = HostEventLauncher.Start();
log.SetNumberOfMilestones(5);
log.Publish("Registering services...");
log.MilestoneReached("Services registered");
```

### Pipe mode (external runner)

When launched by `HostEventLauncher.Runner`, connect automatically from the environment variable:

```csharp
var log = HostEventLauncher.ConnectPipe();
```

Environment variables (runner sets both for compatibility):

- `HOST_EVENT_LAUNCHER_PIPE`
- `IMPORT_ORDERS_FROM_FILE_RUNNER_PIPE` (legacy)

### Message protocol

Tab-separated lines: `type \t timestamp \t text`

| type | text | meaning |
|------|------|---------|
| `log` | message | console log line |
| `progress` | number | set milestone count |
| `progress` | `reached` | advance one milestone |
| `control` | `kill` | runner exits |

## Projects

| Project | Role |
|---------|------|
| `HostEventLauncher.Sharp` | Library — in-process and pipe publishers |
| `HostEventLauncher.Runner` | Console runner that spawns a client and displays its pipe output |
| `HostEventLauncher.Sharp.Tests` | Unit tests |

## Build

```bash
dotnet test HostEventLauncher.slnx
dotnet build HostEventLauncher.Runner/HostEventLauncher.Runner.csproj
```
