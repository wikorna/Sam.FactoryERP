namespace FactoryERP.Abstractions.Realtime;

/// <summary>
/// Tracks progress and determines when the next 10 % threshold has been crossed.
/// Prevents duplicate progress events and guarantees 100 % is emitted before completion.
/// </summary>
/// <remarks>
/// This class is <b>not</b> thread-safe — use one instance per worker loop.
/// </remarks>
public sealed class ProgressThresholdCalculator
{
    private readonly int _stepPercent;
    private int _nextThreshold;
    private int _lastEmittedPercent = -1;

    /// <summary>Creates a new calculator with the specified step size (default 10 %).</summary>
    /// <param name="stepPercent">Percentage increment between emits (must be 1–100).</param>
    public ProgressThresholdCalculator(int stepPercent = 10)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(stepPercent, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(stepPercent, 100);

        _stepPercent = stepPercent;
        _nextThreshold = stepPercent;
    }

    /// <summary>
    /// Calculates whether the current progress crosses the next threshold.
    /// </summary>
    /// <param name="processed">Number of items processed so far.</param>
    /// <param name="total">Total number of items to process.</param>
    /// <param name="percent">The calculated percentage (0–100) if a threshold was crossed.</param>
    /// <returns><c>true</c> if a progress event should be emitted.</returns>
    public bool ShouldEmit(int processed, int total, out int percent)
    {
        percent = CalculatePercent(processed, total);

        if (percent == _lastEmittedPercent)
            return false;

        if (percent >= _nextThreshold)
        {
            // Snap to the threshold boundary to keep emissions aligned.
            while (_nextThreshold <= percent && _nextThreshold <= 100)
                _nextThreshold += _stepPercent;

            _lastEmittedPercent = percent;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Forces a final 100 % emission regardless of threshold state.
    /// Call this just before <c>JobCompleted</c>.
    /// </summary>
    /// <param name="total">Total items — used as both processed and total.</param>
    /// <param name="percent">Always 100.</param>
    /// <returns><c>true</c> if 100 % has not already been emitted.</returns>
    public bool ShouldEmitFinal(int total, out int percent)
    {
        percent = 100;
        if (_lastEmittedPercent == 100)
            return false;

        _lastEmittedPercent = 100;
        return true;
    }

    /// <summary>Resets the calculator for reuse.</summary>
    public void Reset()
    {
        _nextThreshold = _stepPercent;
        _lastEmittedPercent = -1;
    }

    private static int CalculatePercent(int processed, int total)
    {
        if (total <= 0)
            return 0;

        var clamped = Math.Max(0, Math.Min(processed, total));
        return (int)(clamped * 100L / total);
    }
}

