using System.Runtime.InteropServices;

namespace HostEventLauncher.Sharp;

internal static class NativeConsole
{
    private const uint AttachParentProcess = 0xFFFFFFFF;

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AllocConsole();

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AttachConsole(uint dwProcessId);

    public static bool TryAllocate()
    {
        if (!Console.IsOutputRedirected)
        {
            return true;
        }

        if (AttachConsole(AttachParentProcess))
        {
            return true;
        }

        return AllocConsole();
    }
}
