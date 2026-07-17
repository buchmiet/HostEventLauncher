namespace HostEventLauncher.Sharp;

public static class ConsoleHost
{
    public static bool HasConsole()
    {
        if (OperatingSystem.IsWindows())
        {
            return NativeConsole.HasConsoleWindow();
        }

        return !Console.IsOutputRedirected;
    }

    public static bool EnsureAttached() => NativeConsole.TryAllocate();
}
