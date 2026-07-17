using System.Runtime.InteropServices;
using System.Text;

namespace HostEventLauncher.Sharp;

internal static class NativeConsole
{
    private const uint AttachParentProcess = 0xFFFFFFFF;
    private const int SwShow = 5;

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AllocConsole();

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AttachConsole(uint dwProcessId);

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetConsoleWindow();

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    public static bool HasConsoleWindow() => GetConsoleWindow() != IntPtr.Zero;

    public static bool TryAllocate()
    {
        if (GetConsoleWindow() != IntPtr.Zero)
        {
            return true;
        }

        if (AttachConsole(AttachParentProcess))
        {
            BindStandardHandles();
            return true;
        }

        if (!AllocConsole())
        {
            return false;
        }

        BindStandardHandles();
        var consoleWindow = GetConsoleWindow();
        if (consoleWindow != IntPtr.Zero)
        {
            ShowWindow(consoleWindow, SwShow);
        }

        return true;
    }

    private static void BindStandardHandles()
    {
        Console.OutputEncoding = new UTF8Encoding(false);
        Console.InputEncoding = new UTF8Encoding(false);
        Console.SetOut(new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true });
        Console.SetError(new StreamWriter(Console.OpenStandardError()) { AutoFlush = true });
        Console.SetIn(new StreamReader(Console.OpenStandardInput()));
    }
}
