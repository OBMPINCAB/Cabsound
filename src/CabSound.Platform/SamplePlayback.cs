using System.Runtime.InteropServices;
using NAudio.Wave;

namespace CabSound.Platform;

/// <summary>What playing float samples on the sound card needs: a 7.1 layout and a 32-bit float mix format.</summary>
public static class SamplePlayback
{
    /// <summary>Channel mask of 7.1 (FL FR FC LFE BL BR SL SR).</summary>
    public const int Mask71 = 0x63F;

    private static readonly Guid MediaSubtypeIeeeFloat = new("00000003-0000-0010-8000-00aa00389b71");

    /// <summary>WAVEFORMATEXTENSIBLE: dwChannelMask sits at byte 20 (after WAVEFORMATEX + wValidBitsPerSample). 0 = not extensible.</summary>
    public static int ChannelMask(WaveFormat format)
    {
        if (format.Encoding != WaveFormatEncoding.Extensible) return 0;
        var ptr = WaveFormat.MarshalToPtr(format);
        try { return Marshal.ReadInt32(ptr, 20); }
        finally { Marshal.FreeHGlobal(ptr); }
    }

    private static bool IsIeeeFloat(WaveFormat format) =>
        format.Encoding == WaveFormatEncoding.IeeeFloat
        || (format is WaveFormatExtensible ext && ext.SubFormat == MediaSubtypeIeeeFloat);

    /// <summary>null = usable; else why the card's mix format can't be played on.</summary>
    public static string? Problem(WaveFormat format) =>
        ChannelMask(format) != Mask71 ? "Not a 7.1 layout (mask 0x63F expected)"
        : format.BitsPerSample != 32 || !IsIeeeFloat(format) ? "Mix format isn't 32-bit float"
        : null;
}

/// <summary>
/// Feeds interleaved float samples (TestTone.Read, BandNoise.Read, ...) to NAudio as bytes: NAudio's
/// SampleToWaveProvider rejects the device's Extensible float format.
/// </summary>
public sealed class SampleProvider(Func<float[], int, int, int> read, WaveFormat format) : IWaveProvider
{
    private float[] _samples = [];

    public WaveFormat WaveFormat => format;

    public int Read(byte[] buffer, int offset, int count)
    {
        var n = count / 4;
        if (_samples.Length < n) _samples = new float[n];
        var got = read(_samples, 0, n);
        Buffer.BlockCopy(_samples, 0, buffer, offset, got * 4);
        return got * 4;
    }
}
