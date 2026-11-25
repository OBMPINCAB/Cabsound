namespace CabSound.Core;

/// <summary>
/// Groups rapid changes into one write: the include file is due a short delay after the first pending
/// change (Equalizer APO reloads it while it changes), the data file a longer delay after.
/// </summary>
public sealed class WriteScheduler(int includeDelayMs = 300, int dataDelayMs = 2000)
{
    private long? _includeDue, _dataDue;

    /// <summary>Due a short delay after the first pending change, so a held key keeps writing.</summary>
    public void IncludeChanged(long nowMs) => _includeDue ??= nowMs + includeDelayMs;

    public void DataChanged(long nowMs) => _dataDue = nowMs + dataDelayMs;

    /// <summary>What to write now. Each pending change is reported once.</summary>
    public (bool Include, bool Data) Due(long nowMs)
    {
        var include = _includeDue <= nowMs;
        var data = _dataDue <= nowMs;
        if (include) _includeDue = null;
        if (data) _dataDue = null;
        return (include, data);
    }

    /// <summary>Everything pending, regardless of time (for exit).</summary>
    public (bool Include, bool Data) TakeAll()
    {
        var result = (_includeDue is not null, _dataDue is not null);
        _includeDue = _dataDue = null;
        return result;
    }
}
