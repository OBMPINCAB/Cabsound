namespace CabSound.Core;

/// <summary>
/// Hold-to-repeat for button actions: after the first delay, the action repeats every interval
/// until Stop. A late Tick fires once (no burst of catch-up repeats).
/// </summary>
public sealed class RepeatTimer<T>(int firstDelayMs = 400, int intervalMs = 100) where T : notnull
{
    private readonly Dictionary<T, long> _nextDue = new();

    public void Start(T action, long nowMs) => _nextDue[action] = nowMs + firstDelayMs;

    public void Stop(T action) => _nextDue.Remove(action);

    public IReadOnlyList<T> Tick(long nowMs)
    {
        var fired = _nextDue.Where(kv => nowMs >= kv.Value).Select(kv => kv.Key).ToList();
        foreach (var action in fired) _nextDue[action] = nowMs + intervalMs;
        return fired;
    }
}
