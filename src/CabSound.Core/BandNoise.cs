namespace CabSound.Core;

/// <summary>
/// Pink noise limited to a frequency band, for the calibration wizard (phone meter readings) and the
/// exciter limit check. Interleaved float samples, same shape as <see cref="TestTone.Read"/>.
/// Every active channel gets its own noise (identical noise on two speakers makes a meter reading
/// change with small phone movements). -20 dBFS RMS, fades in over 0.5 s.
/// </summary>
public sealed class BandNoise
{
    public const double LevelDb = -20, FadeSeconds = 0.5;
    const float Limit = 0.99f;

    private readonly int _sampleRate, _channels;
    private readonly int[] _active;
    private readonly Voice[] _voices;
    private long _frame;

    public BandNoise(int sampleRate, int channels, int[] activeChannels, double lowHz, double highHz, int seed = 1)
    {
        _active = activeChannels;
        if (_active.Length == 0) throw new ArgumentException("At least one active channel is required.", nameof(activeChannels));
        if (channels <= _active.Max()) throw new ArgumentException($"Device has {channels} channels; this needs 8 (7.1)");
        _sampleRate = sampleRate;
        _channels = channels;
        _voices = [.. _active.Select(ch => Voice.AtLevel(sampleRate, lowHz, highHz, seed + ch))];
    }

    /// <summary>Level noise, 500 Hz to 2 kHz: above every low cut, where a phone microphone is trustworthy.</summary>
    public static BandNoise Level(int sampleRate, int channels, int[] activeChannels, int seed = 1) =>
        new(sampleRate, channels, activeChannels, 500, 2000, seed);

    /// <summary>Low noise, 30 to 200 Hz: for finding where the exciters rattle.</summary>
    public static BandNoise Low(int sampleRate, int channels, int[] activeChannels, int seed = 1) =>
        new(sampleRate, channels, activeChannels, 30, 200, seed);

    public int Read(float[] buffer, int offset, int count)
    {
        var frames = count / _channels;
        Array.Clear(buffer, offset, frames * _channels);
        for (var f = 0; f < frames; f++, _frame++)
        {
            var fade = Math.Min(1.0, _frame / (FadeSeconds * _sampleRate));
            for (var i = 0; i < _active.Length; i++)
                buffer[offset + f * _channels + _active[i]] = Math.Clamp((float)(fade * _voices[i].Next()), -Limit, Limit);
        }
        return frames * _channels;
    }

    /// <summary>One channel's noise: white → pink → band-pass, scaled to the wanted level.</summary>
    private sealed class Voice(int sampleRate, double lowHz, double highHz, int seed)
    {
        private readonly Random _random = new(seed);
        private readonly Biquad[] _filters =
        [
            Biquad.HighPass(sampleRate, lowHz), Biquad.HighPass(sampleRate, lowHz),
            Biquad.LowPass(sampleRate, highHz), Biquad.LowPass(sampleRate, highHz)
        ];
        private double _b0, _b1, _b2, _gain = 1;

        /// <summary>
        /// A voice whose level is <see cref="LevelDb"/> RMS: a copy with the same seed is measured first
        /// (2 s, after the fade time), so the gain fits these very samples.
        /// </summary>
        public static Voice AtLevel(int sampleRate, double lowHz, double highHz, int seed)
        {
            var probe = new Voice(sampleRate, lowHz, highHz, seed);
            var skip = (int)(FadeSeconds * sampleRate);
            var frames = 2 * sampleRate;
            for (var i = 0; i < skip; i++) probe.Next();
            double sum = 0;
            for (var i = 0; i < frames; i++) { var x = probe.Next(); sum += x * x; }
            var rms = Math.Sqrt(sum / frames);
            return new Voice(sampleRate, lowHz, highHz, seed) { _gain = rms > 0 ? Math.Pow(10, LevelDb / 20) / rms : 0 };
        }

        public double Next()
        {
            // Paul Kellett's "economy" pink filter
            var white = _random.NextDouble() * 2 - 1;
            _b0 = 0.99765 * _b0 + white * 0.0990460;
            _b1 = 0.96300 * _b1 + white * 0.2965164;
            _b2 = 0.57000 * _b2 + white * 1.0526913;
            var x = _b0 + _b1 + _b2 + white * 0.1848;
            foreach (var filter in _filters) x = filter.Next(x);
            return x * _gain;
        }
    }

    /// <summary>Second-order Butterworth filter (RBJ cookbook, Q 0.7071).</summary>
    private sealed class Biquad(double b0, double b1, double b2, double a1, double a2)
    {
        const double Q = 0.7071;
        private double _x1, _x2, _y1, _y2;

        public static Biquad HighPass(int sampleRate, double hz)
        {
            var (cos, alpha) = Terms(sampleRate, hz);
            var a0 = 1 + alpha;
            return new((1 + cos) / 2 / a0, -(1 + cos) / a0, (1 + cos) / 2 / a0, -2 * cos / a0, (1 - alpha) / a0);
        }

        public static Biquad LowPass(int sampleRate, double hz)
        {
            var (cos, alpha) = Terms(sampleRate, hz);
            var a0 = 1 + alpha;
            return new((1 - cos) / 2 / a0, (1 - cos) / a0, (1 - cos) / 2 / a0, -2 * cos / a0, (1 - alpha) / a0);
        }

        private static (double Cos, double Alpha) Terms(int sampleRate, double hz)
        {
            var w = 2 * Math.PI * hz / sampleRate;
            return (Math.Cos(w), Math.Sin(w) / (2 * Q));
        }

        public double Next(double x)
        {
            var y = b0 * x + b1 * _x1 + b2 * _x2 - a1 * _y1 - a2 * _y2;
            (_x2, _x1, _y2, _y1) = (_x1, x, _y1, y);
            return y;
        }
    }
}
