using System.Text;

namespace CabSound.Core;

/// <summary>Where VPX plays a sound: Table = positioned on the playfield (exciters), Backglass = plain stereo (backbox).</summary>
public enum SoundOutput { Table = 0, Backglass = 1 }

/// <summary>
/// One GameStg/SoundN stream of a .vpx (file version 10.8): name, path, a dummy string, WAVEFORMATEX (18 bytes, .wav
/// only), int length + data, then 17 bytes (byte output, int volume, pan, fade, volume). Sound streams are not part of
/// the table hash, so changing one keeps the table valid.
/// </summary>
public sealed class VpxSound
{
    private const int TailLength = 17;
    private readonly byte[] _dummy, _format, _tail;

    public string Name { get; }
    public string Path { get; }
    public byte[] Data { get; }
    public bool IsWav => _format.Length == 18;
    public int FormatTag => IsWav ? BitConverter.ToUInt16(_format, 0) : 0;
    public int Channels => IsWav ? BitConverter.ToUInt16(_format, 2) : 0;
    public int Rate => IsWav ? BitConverter.ToInt32(_format, 4) : 0;
    public int Bits => IsWav ? BitConverter.ToUInt16(_format, 14) : 0;
    public double DurationSeconds => IsWav && Channels > 0 && Bits > 0 && Rate > 0
        ? Data.Length / (double)(Rate * Channels * (Bits / 8))
        : 0;
    public SoundOutput Output => _tail[0] == 1 ? SoundOutput.Backglass : SoundOutput.Table;
    public SoundInfo Info => new(Name, Output, Channels, Bits, IsWav, SoundAudio.Facts(this));

    /// <summary>Stereo 16-bit PCM table sounds: VPX's SSF mode positions mono sounds best.</summary>
    public bool CanMakeMono => IsWav && Channels == 2 && Bits == 16 && Output == SoundOutput.Table;

    private VpxSound(string name, string path, byte[] dummy, byte[] format, byte[] data, byte[] tail)
    {
        Name = name; Path = path; _dummy = dummy; _format = format; Data = data; _tail = tail;
    }

    public static VpxSound Parse(byte[] stream)
    {
        try
        {
            var p = 0;
            byte[] Bytes(int n) { var b = stream[p..(p + n)]; p += n; return b; }
            byte[] Sized() { var n = BitConverter.ToInt32(stream, p); p += 4; return Bytes(n); }
            var name = Encoding.Latin1.GetString(Sized());
            var path = Encoding.Latin1.GetString(Sized());
            var dummy = Sized();
            var format = path.EndsWith(".wav", StringComparison.OrdinalIgnoreCase) ? Bytes(18) : [];
            var data = Sized();
            if (stream.Length - p != TailLength) throw new InvalidDataException($"Sound '{name}': {stream.Length - p} bytes after the data, expected {TailLength}");
            return new VpxSound(name, path, dummy, format, data, Bytes(TailLength));
        }
        catch (ArgumentException e) { throw new InvalidDataException("Sound stream too short", e); }
    }

    public byte[] Write()
    {
        var ms = new MemoryStream();
        var w = new BinaryWriter(ms);
        void Sized(byte[] b) { w.Write(b.Length); w.Write(b); }
        Sized(Encoding.Latin1.GetBytes(Name));
        Sized(Encoding.Latin1.GetBytes(Path));
        Sized(_dummy);
        w.Write(_format);
        Sized(Data);
        w.Write(_tail);
        return ms.ToArray();
    }

    /// <summary>Same sound with another output: Backglass = plain stereo on the backbox (for callouts and game effects).</summary>
    public VpxSound WithOutput(SoundOutput output)
    {
        var tail = (byte[])_tail.Clone();
        tail[0] = (byte)output;
        return new VpxSound(Name, Path, _dummy, _format, Data, tail);
    }

    /// <summary>This sound with the audio (format + data) of an older copy of it, e.g. stereo from a backup;
    /// output and volume/pan/fade settings stay as they are now.</summary>
    public VpxSound WithAudioFrom(VpxSound older)
    {
        if (!older.Name.Equals(Name, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException($"{older.Name} is not {Name}", nameof(older));
        return new VpxSound(Name, Path, older._dummy, older._format, older.Data, _tail);
    }

    /// <summary>Case-insensitive; a pattern ending in * matches names starting with the rest.</summary>
    public static bool NameMatches(string name, IEnumerable<string> patterns) => patterns.Any(p =>
        p.EndsWith('*') ? name.StartsWith(p[..^1], StringComparison.OrdinalIgnoreCase) : name.Equals(p, StringComparison.OrdinalIgnoreCase));

    /// <summary>(L+R)/2 per sample; name, path and the sound's volume/pan/fade settings stay. Peaks are absolute sample values.</summary>
    public (VpxSound Mono, int PeakIn, int PeakOut) MakeMono()
    {
        if (!CanMakeMono) throw new InvalidOperationException($"Sound '{Name}' is not a stereo 16-bit table sound");
        var frames = Data.Length / 4;
        var mono = new byte[frames * 2];
        int peakIn = 0, peakOut = 0;
        for (var i = 0; i < frames; i++)
        {
            int l = BitConverter.ToInt16(Data, i * 4), r = BitConverter.ToInt16(Data, i * 4 + 2);
            var m = (l + r) / 2;
            peakIn = Math.Max(peakIn, Math.Max(Math.Abs(l), Math.Abs(r)));
            peakOut = Math.Max(peakOut, Math.Abs(m));
            BitConverter.TryWriteBytes(mono.AsSpan(i * 2), (short)m);
        }
        var format = (byte[])_format.Clone();
        BitConverter.TryWriteBytes(format.AsSpan(2), (ushort)1);
        BitConverter.TryWriteBytes(format.AsSpan(8), Rate * 2);
        BitConverter.TryWriteBytes(format.AsSpan(12), (ushort)2);
        return (new VpxSound(Name, Path, _dummy, format, mono, _tail), peakIn, peakOut);
    }

    /// <summary>
    /// Replaces a silent dummy or unused WAV sound stream with a clean 16-bit mono PCM tactile shaker pulse
    /// (default 38 Hz on Table output so it drives exciters + CSD bass shaker). Keeps Name, Path and volume/pan/fade tail.
    /// </summary>
    public VpxSound WithShakerPulse(double? seconds = null, double hz = 38, double peakDb = -1.5)
    {
        if (!IsWav) throw new InvalidOperationException($"Sound '{Name}' is not a .wav stream");
        var rate = Rate > 0 ? Rate : 44100;
        var dur = Math.Clamp(seconds ?? (DurationSeconds > 0.05 ? DurationSeconds : 0.35), 0.2, 2.5);
        var frames = Math.Max(1, (int)Math.Round(rate * dur));
        var amp = Math.Pow(10, Math.Min(0, peakDb) / 20.0) * 32767.0;
        var fadeSec = Math.Min(0.04, dur * 0.25);
        var pcm = new byte[frames * 2];

        for (var i = 0; i < frames; i++)
        {
            var t = i / (double)rate;
            var env = t < fadeSec ? 0.5 * (1 - Math.Cos(Math.PI * t / fadeSec))
                    : t > dur - fadeSec ? 0.5 * (1 - Math.Cos(Math.PI * (dur - t) / fadeSec))
                    : 1.0;
            // Longer saucer/motor shakes get subtle 14 Hz eccentricity modulation so they feel like a real spinning weight
            var motorMod = dur >= 0.45 ? 0.85 + 0.15 * Math.Sin(2 * Math.PI * 14.0 * t) : 1.0;
            var sample = (int)Math.Round(amp * env * motorMod * Math.Sin(2 * Math.PI * hz * t));
            BitConverter.TryWriteBytes(pcm.AsSpan(i * 2), (short)Math.Clamp(sample, -32767, 32767));
        }

        var format = new byte[18];
        BitConverter.TryWriteBytes(format.AsSpan(0), (ushort)1);          // PCM
        BitConverter.TryWriteBytes(format.AsSpan(2), (ushort)1);          // 1 channel (mono)
        BitConverter.TryWriteBytes(format.AsSpan(4), rate);               // sample rate
        BitConverter.TryWriteBytes(format.AsSpan(8), rate * 2);           // bytes/sec
        BitConverter.TryWriteBytes(format.AsSpan(12), (ushort)2);         // block align
        BitConverter.TryWriteBytes(format.AsSpan(14), (ushort)16);        // 16-bit

        var tail = (byte[])_tail.Clone();
        tail[0] = (byte)SoundOutput.Table;
        return new VpxSound(Name, Path, _dummy, format, pcm, tail);
    }

    /// <summary>
    /// Preserves an existing 16-bit or 24-bit PCM WAV sound (e.g. a plastic UFO/toy rattle that lacks sub-bass)
    /// and mixes a clean 38 Hz tactile shaker layer underneath it, normalized safely below 0 dBFS on Table output.
    /// </summary>
    public VpxSound WithShakerUnderlay(double hz = 38, double underlayDb = -6)
    {
        if (!IsWav || FormatTag != 1 || Bits is not (16 or 24) || Channels is not (1 or 2))
            throw new InvalidOperationException($"Sound '{Name}' must be 16-bit or 24-bit mono/stereo PCM .wav");

        var bytesPerSample = Bits / 8;
        var frameBytes = bytesPerSample * Channels;
        var frames = Data.Length / frameBytes;
        if (frames == 0) return WithShakerPulse(hz: hz);

        var rate = Rate > 0 ? Rate : 44100;
        var dur = frames / (double)rate;
        double fullScale = Bits == 16 ? 32768.0 : 8388608.0;
        var fadeSec = Math.Min(0.04, dur * 0.25);
        var shakerAmp = Math.Pow(10, Math.Min(0, underlayDb) / 20.0);

        var mixed = new double[frames];
        var maxAbs = 0.0;
        for (var i = 0; i < frames; i++)
        {
            double orig;
            if (bytesPerSample == 2)
            {
                double l = BitConverter.ToInt16(Data, i * frameBytes) / fullScale;
                orig = Channels == 1 ? l : 0.5 * (l + BitConverter.ToInt16(Data, i * frameBytes + 2) / fullScale);
            }
            else
            {
                static int Read24(byte[] d, int off)
                {
                    var v = d[off] | (d[off + 1] << 8) | (d[off + 2] << 16);
                    return v >= 0x800000 ? v - 0x1000000 : v;
                }
                double l = Read24(Data, i * frameBytes) / fullScale;
                orig = Channels == 1 ? l : 0.5 * (l + Read24(Data, i * frameBytes + 3) / fullScale);
            }

            var t = i / (double)rate;
            var env = t < fadeSec ? 0.5 * (1 - Math.Cos(Math.PI * t / fadeSec))
                    : t > dur - fadeSec ? 0.5 * (1 - Math.Cos(Math.PI * (dur - t) / fadeSec))
                    : 1.0;
            var motorMod = dur >= 0.45 ? 0.85 + 0.15 * Math.Sin(2 * Math.PI * 14.0 * t) : 1.0;
            var sub = shakerAmp * env * motorMod * Math.Sin(2 * Math.PI * hz * t);
            var sum = orig + sub;
            mixed[i] = sum;
            maxAbs = Math.Max(maxAbs, Math.Abs(sum));
        }

        // Prevent digital clipping if original + 38 Hz underlay exceeds -0.5 dBFS
        var scale = maxAbs > 0.94 ? 0.94 / maxAbs : 1.0;
        var pcm = new byte[frames * 2];
        for (var i = 0; i < frames; i++)
        {
            var s = (int)Math.Round(mixed[i] * scale * 32767.0);
            BitConverter.TryWriteBytes(pcm.AsSpan(i * 2), (short)Math.Clamp(s, -32767, 32767));
        }

        var format = new byte[18];
        BitConverter.TryWriteBytes(format.AsSpan(0), (ushort)1);
        BitConverter.TryWriteBytes(format.AsSpan(2), (ushort)1);
        BitConverter.TryWriteBytes(format.AsSpan(4), rate);
        BitConverter.TryWriteBytes(format.AsSpan(8), rate * 2);
        BitConverter.TryWriteBytes(format.AsSpan(12), (ushort)2);
        BitConverter.TryWriteBytes(format.AsSpan(14), (ushort)16);

        var tail = (byte[])_tail.Clone();
        tail[0] = (byte)SoundOutput.Table;
        return new VpxSound(Name, Path, _dummy, format, pcm, tail);
    }
}
