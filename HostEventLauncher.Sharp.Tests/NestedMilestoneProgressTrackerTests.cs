using HostEventLauncher.Sharp.Milestones;

namespace HostEventLauncher.Sharp.Tests;

public class NestedMilestoneProgressTrackerTests
{
    [Test]
    public async Task RootMilestones_AdvanceProgressByEqualSlices()
    {
        var tracker = new NestedMilestoneProgressTracker();

        var initial = tracker.SetNumberOfMilestones(5);
        var afterFirst = tracker.MilestoneReached("step 1");
        var afterSecond = tracker.MilestoneReached("step 2");
        var afterThird = tracker.MilestoneReached("step 3");
        var afterFourth = tracker.MilestoneReached("step 4");
        var completed = tracker.MilestoneReached("step 5");

        using var _ = Assert.Multiple();
        await Assert.That(initial.ProgressRatio).IsEqualTo(0d);
        await Assert.That(afterFirst.ProgressRatio).IsEqualTo(0.2d).Within(1e-9);
        await Assert.That(afterSecond.ProgressRatio).IsEqualTo(0.4d).Within(1e-9);
        await Assert.That(afterThird.ProgressRatio).IsEqualTo(0.6d).Within(1e-9);
        await Assert.That(afterFourth.ProgressRatio).IsEqualTo(0.8d).Within(1e-9);
        await Assert.That(completed.ProgressRatio).IsEqualTo(1d);
        await Assert.That(completed.CompletedRootMilestones).IsEqualTo(5d);
        await Assert.That(completed.IsCompleted).IsTrue();
    }

    [Test]
    public async Task NestedMilestones_ConsumeOnlyCurrentParentSlice()
    {
        var tracker = new NestedMilestoneProgressTracker();

        tracker.SetNumberOfMilestones(5);
        tracker.MilestoneReached("host 1");
        tracker.MilestoneReached("host 2");

        var nestedStart = tracker.SetNumberOfMilestones(7);

        MilestoneProgressSnapshot afterThirdNested = default;
        for (var i = 0; i < 3; i++)
        {
            afterThirdNested = tracker.MilestoneReached($"child {i + 1}");
        }

        MilestoneProgressSnapshot afterNestedComplete = default;
        for (var i = 3; i < 7; i++)
        {
            afterNestedComplete = tracker.MilestoneReached($"child {i + 1}");
        }

        using var _ = Assert.Multiple();
        await Assert.That(nestedStart.ProgressRatio).IsEqualTo(0.4d).Within(1e-9);
        await Assert.That(afterThirdNested.ProgressRatio).IsEqualTo(0.4d + (0.2d * 3d / 7d)).Within(1e-9);
        await Assert.That(afterNestedComplete.ProgressRatio).IsEqualTo(0.6d).Within(1e-9);
        await Assert.That(afterNestedComplete.CompletedRootMilestones).IsEqualTo(3d).Within(1e-9);
        await Assert.That(afterNestedComplete.IsCompleted).IsFalse();
    }

    [Test]
    public async Task ReachingMilestoneWithoutActivePlan_Throws()
    {
        var tracker = new NestedMilestoneProgressTracker();
        var action = () => { tracker.MilestoneReached("unexpected"); };
        await Assert.That(action).Throws<InvalidOperationException>();
    }
}
