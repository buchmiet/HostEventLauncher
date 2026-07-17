using HostEventLauncher.Sharp.Progress;

namespace HostEventLauncher.Sharp.Tests;

public class NestedProgressTrackerTests
{
    [Test]
    public async Task RootSteps_AdvanceProgressByEqualSlices()
    {
        var tracker = new NestedProgressTracker();

        var initial = tracker.BeginProgress(5);
        var afterFirst = tracker.CompleteStep();
        var afterSecond = tracker.CompleteStep();
        var afterThird = tracker.CompleteStep();
        var afterFourth = tracker.CompleteStep();
        var completed = tracker.CompleteStep();

        using var _ = Assert.Multiple();
        await Assert.That(initial.Ratio).IsEqualTo(0d);
        await Assert.That(afterFirst.Ratio).IsEqualTo(0.2d).Within(1e-9);
        await Assert.That(afterSecond.Ratio).IsEqualTo(0.4d).Within(1e-9);
        await Assert.That(afterThird.Ratio).IsEqualTo(0.6d).Within(1e-9);
        await Assert.That(afterFourth.Ratio).IsEqualTo(0.8d).Within(1e-9);
        await Assert.That(completed.Ratio).IsEqualTo(1d);
        await Assert.That(completed.CompletedRootSteps).IsEqualTo(5d);
        await Assert.That(completed.IsCompleted).IsTrue();
    }

    [Test]
    public async Task NestedSteps_ConsumeOnlyCurrentParentSlice()
    {
        var tracker = new NestedProgressTracker();

        tracker.BeginProgress(5);
        tracker.CompleteStep();
        tracker.CompleteStep();

        var nestedStart = tracker.BeginProgress(7);

        ProgressSnapshot afterThirdNested = default;
        for (var i = 0; i < 3; i++)
        {
            afterThirdNested = tracker.CompleteStep();
        }

        ProgressSnapshot afterNestedComplete = default;
        for (var i = 3; i < 7; i++)
        {
            afterNestedComplete = tracker.CompleteStep();
        }

        using var _ = Assert.Multiple();
        await Assert.That(nestedStart.Ratio).IsEqualTo(0.4d).Within(1e-9);
        await Assert.That(afterThirdNested.Ratio).IsEqualTo(0.4d + (0.2d * 3d / 7d)).Within(1e-9);
        await Assert.That(afterNestedComplete.Ratio).IsEqualTo(0.6d).Within(1e-9);
        await Assert.That(afterNestedComplete.CompletedRootSteps).IsEqualTo(3d).Within(1e-9);
        await Assert.That(afterNestedComplete.IsCompleted).IsFalse();
    }

    [Test]
    public async Task CompletingStepWithoutActivePhase_Throws()
    {
        var tracker = new NestedProgressTracker();
        var action = () => { tracker.CompleteStep(); };
        await Assert.That(action).Throws<InvalidOperationException>();
    }
}
