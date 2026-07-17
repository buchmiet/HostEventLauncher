using HostEventLauncher.Sharp.Progress;
using System.Runtime.InteropServices;
using System.Text;

namespace HostEventLauncher.Sharp;

internal sealed partial class StartupConsoleView : IDisposable
{
    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetConsoleOutputCP(uint wCodePageID);

    [LibraryImport("kernel32.dll")]
    private static partial uint GetConsoleOutputCP();

    public event Action? OnProgressComplete;

    private readonly Lock _sync = new();
    private readonly bool _supportsCursorPositioning = !Console.IsOutputRedirected;
    private readonly DotsSpinner _spinner;
    private CancellationTokenSource? _spinnerCts;
    private Task? _spinnerTask;
    private int _spinnerRow = -1;
    private int _spinnerColumn = 1;
    private int _nextRow;
    private bool _cursorHidden;
    private readonly NestedProgressTracker _progressTracker = new();
    private ProgressSnapshot _progress;
    private bool _completionSignaled;

    public StartupConsoleView()
    {
        var unicodeSpinnerEnabled = TryEnableUnicodeSpinner();
        _spinner = new DotsSpinner(unicodeSpinnerEnabled);
        _nextRow = SafeCursorTop();
    }

    public void WriteLine(string text)
    {
        StopSpinner();

        var message = $"[{DateTime.Now:HH.mm.ss.ffffff}]: {text}";
        if (!_supportsCursorPositioning)
        {
            Console.WriteLine(Truncate(message, SafeWindowWidth()));
            return;
        }

        lock (_sync)
        {
            var row = EnsureOutputRowLocked();
            var width = SafeWindowWidth();
            var visibleText = Truncate(message, width - 2);
            _spinnerColumn = Math.Clamp(visibleText.Length + 1, 0, Math.Max(0, width - 1));

            Console.SetCursorPosition(0, row);
            Console.ForegroundColor = ConsoleColor.White;
            Console.Write(new string(' ', width));
            Console.SetCursorPosition(0, row);
            Console.Write(visibleText);
            Console.ResetColor();
            Console.WriteLine();

            _spinnerRow = row;
            _nextRow = SafeCursorTop();

            if (_progress.RootStepCount > 0)
            {
                DrawProgressBarLocked();
            }
        }
    }

    public void SetProgressTotal(int totalSteps)
    {
        lock (_sync)
        {
            _progress = _progressTracker.BeginProgress(totalSteps);
            if (_progress.ActiveDepth == 1)
            {
                _completionSignaled = false;
            }

            if (totalSteps <= 0 || _spinnerRow < 0 || !_supportsCursorPositioning) return;
            DrawProgressBarLocked();
            RestoreOutputCursorLocked();
        }
    }

    public void AdvanceStep()
    {
        bool completed;

        lock (_sync)
        {
            _progress = _progressTracker.CompleteStep();
            completed = _progress.IsCompleted && !_completionSignaled;

            if (_spinnerRow >= 0 && _supportsCursorPositioning)
            {
                DrawProgressBarLocked();
                RestoreOutputCursorLocked();
            }

            if (completed)
            {
                _completionSignaled = true;
            }
        }

        if (completed)
        {
            OnProgressComplete?.Invoke();
        }
    }

    public void StartSpinner()
    {
        if (!_supportsCursorPositioning)
        {
            return;
        }

        lock (_sync)
        {
            if (_spinnerRow < 0 || _spinnerTask is not null)
            {
                return;
            }

            _spinnerCts = new CancellationTokenSource();
            _spinnerTask = SpinAsync(_spinnerCts.Token);

            if (!_cursorHidden)
            {
                Console.CursorVisible = false;
                _cursorHidden = true;
            }
        }
    }

    public void StopSpinner()
    {
        CancellationTokenSource? spinnerCts;
        Task? spinnerTask;

        lock (_sync)
        {
            spinnerCts = _spinnerCts;
            spinnerTask = _spinnerTask;
            _spinnerCts = null;
            _spinnerTask = null;
        }

        if (spinnerCts is null)
        {
            return;
        }

        spinnerCts.Cancel();

        try
        {
            spinnerTask?.GetAwaiter().GetResult();
        }
        catch (OperationCanceledException)
        {
        }

        lock (_sync)
        {
            ClearSpinnerLocked();

            if (_cursorHidden)
            {
                Console.CursorVisible = true;
                _cursorHidden = false;
            }

            RestoreOutputCursorLocked();
        }

        spinnerCts.Dispose();
    }

    public void Dispose() => StopSpinner();

    private async Task SpinAsync(CancellationToken cancellationToken)
    {
        var frames = _spinner.Frames;
        var frameIndex = 0;

        try
        {
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                lock (_sync)
                {
                    if (_spinnerRow >= 0)
                    {
                        Console.SetCursorPosition(_spinnerColumn, _spinnerRow);
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.Write(frames[frameIndex]);
                        Console.ResetColor();
                        RestoreOutputCursorLocked();
                    }
                }

                frameIndex = (frameIndex + 1) % frames.Count;
                await Task.Delay(_spinner.Interval, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private int EnsureOutputRowLocked()
    {
        var bufferHeight = SafeBufferHeight();
        var row = Math.Clamp(_nextRow, 0, Math.Max(0, bufferHeight - 1));
        Console.SetCursorPosition(0, row);
        return row;
    }

    private void ClearSpinnerLocked()
    {
        if (_spinnerRow < 0)
        {
            return;
        }

        Console.SetCursorPosition(_spinnerColumn, _spinnerRow);
        Console.Write(' ');
    }

    private void DrawProgressBarLocked()
    {
        if (_progress.RootStepCount <= 0 || _spinnerRow < 0)
        {
            return;
        }

        var progressRow = _spinnerRow + 1;
        if (progressRow >= SafeBufferHeight())
        {
            return;
        }

        var width = SafeWindowWidth();
        var percent = (int)Math.Round(_progress.Percent, MidpointRounding.AwayFromZero);
        percent = Math.Clamp(percent, 0, 100);
        var label = $"{percent,3}%";

        var barWidth = Math.Max(10, width - label.Length - 2);
        var filledWidth = (int)Math.Round(_progress.Ratio * barWidth, MidpointRounding.AwayFromZero);
        filledWidth = Math.Clamp(filledWidth, 0, barWidth);
        var emptyWidth = barWidth - filledWidth;

        Console.SetCursorPosition(0, progressRow);
        Console.ForegroundColor = ConsoleColor.DarkCyan;
        Console.Write(new string('█', filledWidth));
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.Write(new string('░', emptyWidth));
        Console.ResetColor();
        Console.Write($" {label}");

        var totalWritten = barWidth + 1 + label.Length;
        if (totalWritten < width)
        {
            Console.Write(new string(' ', width - totalWritten));
        }
    }

    private void RestoreOutputCursorLocked()
    {
        var bufferHeight = SafeBufferHeight();
        var row = Math.Clamp(_nextRow, 0, Math.Max(0, bufferHeight - 1));
        Console.SetCursorPosition(0, row);
    }

    private static string Truncate(string text, int maxWidth)
    {
        if (maxWidth <= 0)
        {
            return string.Empty;
        }

        if (text.Length <= maxWidth)
        {
            return text;
        }

        if (maxWidth <= 3)
        {
            return text[..maxWidth];
        }

        return text[..(maxWidth - 3)] + "...";
    }

    private static int SafeWindowWidth()
    {
        try
        {
            return Math.Max(40, Console.WindowWidth);
        }
        catch
        {
            return 120;
        }
    }

    private static int SafeBufferHeight()
    {
        try
        {
            return Math.Max(1, Console.BufferHeight);
        }
        catch
        {
            return 1_000;
        }
    }

    private static int SafeCursorTop()
    {
        try
        {
            return Console.CursorTop;
        }
        catch
        {
            return 0;
        }
    }

    private static bool TryEnableUnicodeSpinner()
    {
        var forceAscii = Environment.GetEnvironmentVariable("HOST_EVENT_LAUNCHER_ASCII_SPINNER");
        if (string.Equals(forceAscii, "1", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(forceAscii, "true", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        try
        {
            SetConsoleOutputCP(65001);
            Console.OutputEncoding = new UTF8Encoding(false);
            Console.InputEncoding = new UTF8Encoding(false);
            return GetConsoleOutputCP() == 65001;
        }
        catch
        {
            return false;
        }
    }

    private sealed class DotsSpinner(bool useUnicode)
    {
        public TimeSpan Interval => TimeSpan.FromMilliseconds(80);

        public IReadOnlyList<string> Frames { get; } = useUnicode
            ? ["⠋", "⠙", "⠹", "⠸", "⠼", "⠴", "⠦", "⠧", "⠇", "⠏"]
            : [".  ", ".. ", "...", " ..", "  .", "   "];
    }
}
