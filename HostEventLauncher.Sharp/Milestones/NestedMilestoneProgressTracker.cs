namespace HostEventLauncher.Sharp.Milestones;

public sealed class NestedMilestoneProgressTracker
{
    private readonly Stack<Scope> _scopes = new();
    private int _rootMilestoneCount;
    private bool _isCompleted;

    public MilestoneProgressSnapshot Current => CreateSnapshot();

    public MilestoneProgressSnapshot SetNumberOfMilestones(int number)
    {
        if (number <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(number), number, "Milestone count must be greater than zero.");
        }

        if (_scopes.Count == 0)
        {
            _rootMilestoneCount = number;
            _isCompleted = false;
            _scopes.Push(new Scope(number, 1d));
            return CreateSnapshot();
        }

        var current = _scopes.Peek();
        if (current.IsCompleted)
        {
            throw new InvalidOperationException("Cannot create nested milestones for a completed scope.");
        }

        _scopes.Push(new Scope(number, current.Weight / current.TotalMilestones));
        return CreateSnapshot();
    }

    public MilestoneProgressSnapshot MilestoneReached(string? text = null)
    {
        if (_scopes.Count == 0)
        {
            throw new InvalidOperationException("No active milestones. Call SetNumberOfMilestones before MilestoneReached.");
        }

        _scopes.Peek().CompleteMilestone();
        BubbleCompletedScopes();
        return CreateSnapshot();
    }

    private void BubbleCompletedScopes()
    {
        while (_scopes.Count > 0 && _scopes.Peek().IsCompleted)
        {
            _scopes.Pop();

            if (_scopes.Count == 0)
            {
                _isCompleted = true;
                return;
            }

            _scopes.Peek().CompleteMilestone();
        }
    }

    private MilestoneProgressSnapshot CreateSnapshot()
    {
        if (_rootMilestoneCount == 0)
        {
            return new MilestoneProgressSnapshot(0d, 0d, 0, false, 0);
        }

        if (_isCompleted)
        {
            return new MilestoneProgressSnapshot(1d, _rootMilestoneCount, _rootMilestoneCount, true, 0);
        }

        var progress = _scopes.Sum(scope => scope.Weight * scope.CompletedMilestones / scope.TotalMilestones);
        progress = Math.Clamp(progress, 0d, 1d);

        return new MilestoneProgressSnapshot(
            progress,
            progress * _rootMilestoneCount,
            _rootMilestoneCount,
            false,
            _scopes.Count);
    }

    private sealed class Scope(int totalMilestones, double weight)
    {
        public int TotalMilestones { get; } = totalMilestones;

        public double Weight { get; } = weight;

        public int CompletedMilestones { get; private set; }

        public bool IsCompleted => CompletedMilestones >= TotalMilestones;

        public void CompleteMilestone()
        {
            if (IsCompleted)
            {
                throw new InvalidOperationException("MilestoneReached called more times than declared milestones.");
            }

            CompletedMilestones++;
        }
    }
}

public readonly record struct MilestoneProgressSnapshot(
    double ProgressRatio,
    double CompletedRootMilestones,
    int RootMilestoneCount,
    bool IsCompleted,
    int ActiveDepth)
{
    public double ProgressPercent => ProgressRatio * 100d;
}
