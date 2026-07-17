# HostEventLauncher

A tiny **startup reporter** for .NET hosts — timestamped boot lines and an optional progress bar.

Not a general logger (use Serilog/NLog at runtime). Not a splash screen.

---

## Quick start

```csharp
public static void Main(string[] args)
{
    using var boot = Startup.Open(ref args);   // strips --HostEventLogger … from args

    boot.BeginProgress(4);
    boot.Write("Starting…");
    boot.CompleteStep("Host built");

    RunApp(args);   // remaining CLI args
}
```

### Command line

```text
MyApp.exe --HostEventLogger console
MyApp.exe --HostEventLogger file C:\temp\boot.log
MyApp.exe                                    → logger disabled
```

| Flag | Behaviour |
|------|-----------|
| `--HostEventLogger console` | Text + progress bar in a console (allocates one on Windows GUI) |
| `--HostEventLogger file <path>` | Append-style boot log file |
| *(none)* | `NullStartupSession` — zero overhead |

---

## Host types

| Host | `--HostEventLogger console` |
|------|----------------------------|
| Console `Exe` | log in the same terminal |
| GUI `WinExe` | separate console window (`AllocConsole`) |
| Any + `file` | no console needed |

**Same `Main` code for all hosts** — only CLI differs.

---

## API

```csharp
Startup.Open(ref string[] args)              // parse CLI + open session
Startup.Open(HostLoggerLaunchOptions)      // programmatic
Startup.Attach(string? attachName = null)    // remote runner / pipe

IStartupSession
  bool IsEnabled
  void Write(string message)
  void BeginProgress(int totalSteps)
  void CompleteStep(string? message = null)
  void Close()
```

```csharp
ConsoleHost.HasConsole()
ConsoleHost.EnsureAttached()   // used internally for console sink
```

---

## Projects

| Project | Role |
|---------|------|
| `HostEventLauncher.Sharp` | Library |
| `HostEventLauncher.Runner` | Optional detached-console launcher |
| `HostEventLauncher.Sharp.Tests` | Unit tests |

## Build

```bash
dotnet build HostEventLauncher.slnx -c Release
dotnet run --project HostEventLauncher.Sharp.Tests -c Release
```
