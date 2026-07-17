using System.Globalization;

namespace HostEventLauncher.Sharp;

internal readonly record struct StartupWireMessage(
    string Kind,
    string TimestampUtc,
    string Payload)
{
    public static StartupWireMessage BeginProgress(int totalSteps) =>
        new("progress", DateTime.UtcNow.ToString("O"), Sanitize(totalSteps.ToString()));

    public static StartupWireMessage CompleteStep() =>
        new("progress", DateTime.UtcNow.ToString("O"), Sanitize("step"));

    public static StartupWireMessage CreateLog(StartupEntry entry) =>
        new("log", entry.Timestamp.ToUniversalTime().ToString("O"), Sanitize(entry.Text));

    public static StartupWireMessage CreateControl(string command) =>
        new("control", DateTime.UtcNow.ToString("O"), Sanitize(command));

    public string Serialize() => string.Join('\t', Kind, TimestampUtc, Payload);

    public static bool TryParse(string line, out StartupWireMessage message)
    {
        var parts = line.Split('\t', 3, StringSplitOptions.None);
        if (parts.Length != 3)
        {
            message = default;
            return false;
        }

        if (!DateTime.TryParse(
                parts[1],
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out var timestampUtc))
        {
            message = default;
            return false;
        }

        message = new StartupWireMessage(parts[0], timestampUtc.ToString("O"), parts[2]);
        return true;
    }

    private static string Sanitize(string? text) =>
        (text ?? string.Empty)
            .Replace("\r", " ", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal)
            .Replace("\t", "    ", StringComparison.Ordinal);
}

internal readonly record struct StartupEntry(DateTime Timestamp, string Text);
