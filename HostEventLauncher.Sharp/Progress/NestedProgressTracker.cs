namespace HostEventLauncher.Sharp.Progress;

internal sealed class NestedProgressTracker
{
    private readonly Stack<Scope> _scopes = new();
    private int _rootStepCount;
    private bool _isCompleted;

    public ProgressSnapshot Current => CreateSnapshot();

    public ProgressSnapshot BeginProgress(int totalSteps)
    {
        if (totalSteps <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(totalSteps), totalSteps, "Step count must be greater than zero.");
        }

        if (_scopes.Count == 0)
        {
            _rootStepCount = totalSteps;
            _isCompleted = false;
            _scopes.Push(new Scope(totalSteps, 1d));
            return CreateSnapshot();
        }

        var current = _scopes.Peek();
        if (current.IsCompleted)
        {
            throw new InvalidOperationException("Cannot begin a nested progress phase after the current phase is complete.");
        }

        _scopes.Push(new Scope(totalSteps, current.Weight / current.TotalSteps));
        return CreateSnapshot();
    }

    public ProgressSnapshot CompleteStep()
    {
        if (_scopes.Count == 0)
        {
            throw new InvalidOperationException("No active progress phase. Call BeginProgress before CompleteStep.");
        }

        _scopes.Peek().CompleteStep();
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

            _scopes.Peek().CompleteStep();
        }
    }

    private ProgressSnapshot CreateSnapshot()
    {
        if (_rootStepCount == 0)
        {
            return new ProgressSnapshot(0d, 0d, 0, false, 0);
        }

        if (_isCompleted)
        {
            return new ProgressSnapshot(1d, _rootStepCount, _rootStepCount, true, 0);
        }

        var progress = _scopes.Sum(scope => scope.Weight * scope.CompletedSteps / scope.TotalSteps);
        progress = Math.Clamp(progress, 0d, 1d);

        return new ProgressSnapshot(
            progress,
            progress * _rootStepCount,
            _rootStepCount,
            false,
            _scopes.Count);
    }

    private sealed class Scope(int totalSteps, double weight)
    {
        public int TotalSteps { get; } = totalSteps;

        public double Weight { get; } = weight;

        public int CompletedSteps { get; private set; }

        public bool IsCompleted => CompletedSteps >= TotalSteps;

        public void CompleteStep()
        {
            if (IsCompleted)
            {
                throw new InvalidOperationException("CompleteStep called more times than declared steps.");
            }

            CompletedSteps++;
        }
    }
}

internal readonly record struct ProgressSnapshot(
    double Ratio,
    double CompletedRootSteps,
    int RootStepCount,
    bool IsCompleted,
    int ActiveDepth)
{
    public double Percent => Ratio * 100d;
}
