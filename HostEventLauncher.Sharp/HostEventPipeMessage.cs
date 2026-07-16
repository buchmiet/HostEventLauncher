using System.Globalization;

namespace HostEventLauncher.Sharp;

public readonly record struct HostEventPipeMessage(
    string MessageType,
    string TimestampUtc,
    string Text)
{
    public static HostEventPipeMessage SetNumberOfMilestones(int number) =>
        new(
            "progress",
            DateTime.UtcNow.ToString("O"),
            Sanitize(number.ToString()));

    public static HostEventPipeMessage ReachMilestone() =>
        new(
            "progress",
            DateTime.UtcNow.ToString("O"),
            Sanitize("reached"));

    public static HostEventPipeMessage CreateLog(HostEventEntry entry) =>
        new(
            "log",
            entry.Timestamp.ToUniversalTime().ToString("O"),
            Sanitize(entry.Text));

    public static HostEventPipeMessage CreateControl(string command) =>
        new(
            "control",
            DateTime.UtcNow.ToString("O"),
            Sanitize(command));

    public string Serialize() => string.Join('\t', MessageType, TimestampUtc, Text);

    public static bool TryParse(string payload, out HostEventPipeMessage message)
    {
        var parts = payload.Split('\t', 3, StringSplitOptions.None);
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

        message = new HostEventPipeMessage(parts[0], timestampUtc.ToString("O"), parts[2]);
        return true;
    }

    private static string Sanitize(string? text) =>
        (text ?? string.Empty)
            .Replace("\r", " ", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal)
            .Replace("\t", "    ", StringComparison.Ordinal);
}

public readonly record struct HostEventEntry(DateTime Timestamp, string Text);
