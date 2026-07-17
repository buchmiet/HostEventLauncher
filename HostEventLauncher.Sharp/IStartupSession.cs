namespace HostEventLauncher.Sharp;

/// <summary>
/// A startup-phase reporting session: text lines plus an optional progress bar.
/// </summary>
public interface IStartupSession : IDisposable
{
    bool IsEnabled { get; }

    /// <summary>Writes a timestamped line to the startup console.</summary>
    void Write(string message);

    /// <summary>
    /// Declares how many <see cref="CompleteStep"/> calls are required to finish the current progress phase.
    /// Calling again before the phase ends starts a nested sub-phase that consumes one parent step.
    /// </summary>
    void BeginProgress(int totalSteps);

    /// <summary>
    /// Advances the progress bar by one step. When <paramref name="message"/> is provided it is written first.
    /// </summary>
    void CompleteStep(string? message = null);

    /// <summary>Signals that startup reporting is finished and the console may close.</summary>
    void Close();
}
