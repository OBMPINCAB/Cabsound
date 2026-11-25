namespace CabSound.Core;

/// <summary>Card outputs a test tone can go to (one amp input pair each).</summary>
public enum ToneTarget { Backbox, FrontExciters, RearExciters }

/// <summary>
/// Sine tone on one channel pair of a multichannel device, for the bass routing test.
/// Interleaved float samples (same shape as NAudio's ISampleProvider.Read). Fades in over
/// 100 ms so exciters don't thump; levels above -6 dBFS are refused.
/// </summary>
public sealed class TestTone
{
    public const double MaxLevelDb = -6;
    const double FadeSeconds = 0.1;

    private readonly int _sampleRate, _channels;
    private readonly int[] _active;
    private readonly double _frequency, _amplitude;
    private long _frame;

    public TestTone(int sampleRate, int channels, ToneTarget target, double frequencyHz, double levelDb)
        : this(sampleRate, channels, ChannelsFor(target), frequencyHz, levelDb) { }

    /// <summary>Tone on chosen channel indexes (7.1 order), e.g. one channel for the channel test.</summary>
    public TestTone(int sampleRate, int channels, int[] activeChannels, double frequencyHz, double levelDb)
    {
        if (levelDb > MaxLevelDb) throw new ArgumentOutOfRangeException(nameof(levelDb), $"Max is {MaxLevelDb} dBFS");
        _active = activeChannels;
        if (channels <= _active.Max()) throw new ArgumentException($"Device has {channels} channels; this needs 8 (7.1)");
        _sampleRate = sampleRate;
        _channels = channels;
        _frequency = frequencyHz;
        _amplitude = Math.Pow(10, levelDb / 20);
    }

    /// <summary>
    /// Channel indexes in 7.1 order (FL FR FC LFE BL BR SL SR). SSF standard, as VPX sends it:
    /// RL RR (4 5) = front exciters near the player, SL SR (6 7) = rear exciters near the backbox.
    /// </summary>
    public static int[] ChannelsFor(ToneTarget target) => target switch
    {
        ToneTarget.Backbox => [0, 1],
        ToneTarget.FrontExciters => [4, 5],
        ToneTarget.RearExciters => [6, 7],
        _ => throw new ArgumentOutOfRangeException(nameof(target))
    };

    private bool _chime;
    private bool _pulse;
    private double _strikesPerSecond = 1;
    private double _pulseSeconds = 0.25;

    /// <summary>
    /// Bell-like ping that fades out, repeating: easy to tell where it comes from. Different pitch and
    /// strike rate per channel make each channel recognisable.
    /// </summary>
    public static TestTone Chime(int sampleRate, int channels, int[] activeChannels, double levelDb,
        double hz = 880, double strikesPerSecond = 1) =>
        new(sampleRate, channels, activeChannels, hz, levelDb) { _chime = true, _strikesPerSecond = strikesPerSecond };

    /// <summary>
    /// Smooth raised-cosine low-frequency burst followed by silence, repeating every periodSeconds:
    /// feels like a real kick drum (70 Hz) or shaker/bumper thump (42 Hz) without click harmonics.
    /// </summary>
    public static TestTone Pulse(int sampleRate, int channels, int[] activeChannels, double levelDb,
        double hz, double pulseSeconds = 0.25, double periodSeconds = 1.0) =>
        new(sampleRate, channels, activeChannels, hz, levelDb)
        {
            _pulse = true,
            _pulseSeconds = pulseSeconds,
            _strikesPerSecond = 1.0 / periodSeconds,
        };

    public int Read(float[] buffer, int offset, int count)
    {
        var frames = count / _channels;
        Array.Clear(buffer, offset, frames * _channels);
        for (var f = 0; f < frames; f++, _frame++)
        {
            var fade = _pulse ? PulseEnvelope() : _chime ? ChimeEnvelope() : Math.Min(1.0, _frame / (FadeSeconds * _sampleRate));
            var sample = (float)(_amplitude * fade * Math.Sin(2 * Math.PI * _frequency * _frame / _sampleRate));
            foreach (var ch in _active) buffer[offset + f * _channels + ch] = sample;
        }
        return frames * _channels;
    }

    // Strike (5 ms attack, no click) then exponential decay, repeating at the strike rate
    private double ChimeEnvelope()
    {
        var period = (long)(_sampleRate / _strikesPerSecond);
        var t = (double)(_frame % period) / _sampleRate;
        return Math.Min(1.0, t / 0.005) * Math.Exp(-6 * t);
    }

    // Raised-cosine Hann window over pulseSeconds, 0 between bursts (zero click, clear punch & decay)
    private double PulseEnvelope()
    {
        var period = (long)(_sampleRate / _strikesPerSecond);
        var t = (double)(_frame % period) / _sampleRate;
        if (t >= _pulseSeconds) return 0;
        return 0.5 * (1.0 - Math.Cos(2 * Math.PI * t / _pulseSeconds));
    }
}
