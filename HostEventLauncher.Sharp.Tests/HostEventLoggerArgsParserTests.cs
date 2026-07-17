using HostEventLauncher.Sharp;

namespace HostEventLauncher.Sharp.Tests;

public class HostEventLoggerArgsParserTests
{
    [Test]
    public async Task ParseAndRemove_Console_RemovesSwitchAndLeavesOtherArgs()
    {
        var args = new[] { "import", "--HostEventLogger", "console", "--path", "C:\\data" };

        var launch = HostEventLoggerArgsParser.ParseAndRemove(ref args);

        using var _ = Assert.Multiple();
        await Assert.That(launch.Sink).IsEqualTo(HostLoggerSink.Console);
        await Assert.That(args).IsEquivalentTo(["import", "--path", "C:\\data"]);
    }

    [Test]
    public async Task ParseAndRemove_File_RemovesSwitchAndPath()
    {
        var args = new[] { "--HostEventLogger", "file", "boot.log", "run" };

        var launch = HostEventLoggerArgsParser.ParseAndRemove(ref args);

        using var _ = Assert.Multiple();
        await Assert.That(launch.Sink).IsEqualTo(HostLoggerSink.File);
        await Assert.That(launch.FilePath).IsEqualTo("boot.log");
        await Assert.That(args).IsEquivalentTo(["run"]);
    }

    [Test]
    public async Task ParseAndRemove_WithoutSwitch_ReturnsNoneAndKeepsArgs()
    {
        var args = new[] { "import", "--path", "C:\\data" };

        var launch = HostEventLoggerArgsParser.ParseAndRemove(ref args);

        using var _ = Assert.Multiple();
        await Assert.That(launch.Sink).IsEqualTo(HostLoggerSink.None);
        await Assert.That(args).IsEquivalentTo(["import", "--path", "C:\\data"]);
    }

    [Test]
    public async Task Open_RemovesLoggerArgsFromRefArray()
    {
        var args = new[] { "left", "--HostEventLogger", "file", "boot.log", "right" };

        using var session = Startup.Open(ref args);

        using var _ = Assert.Multiple();
        await Assert.That(session.IsEnabled).IsTrue();
        await Assert.That(args).IsEquivalentTo(["left", "right"]);
    }
}
