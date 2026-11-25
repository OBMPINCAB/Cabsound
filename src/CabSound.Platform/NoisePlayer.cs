using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace CabSound.Platform;

/// <summary>
/// Plays a sample source on the cabinet's sound card (WASAPI shared mode, so it goes through Equalizer
/// APO like a table's sound). One source at a time; for the speaker check and the Probe.
/// Nothing here throws: a problem comes back as text.
/// </summary>
public sealed class NoisePlayer(string? deviceId, string deviceName) : IDisposable
{
    private readonly object _lock = new();
    private WasapiOut? _output;
    private MMDevice? _device;

    /// <summary>Playback stopped by an error after it started (e.g. the card was unplugged): the reason.</summary>
    public event Action<string>? Failed;

    /// <summary>
    /// Stops what plays and starts this source. source gets the sample rate and channel count of the card's
    /// mix format and returns the Read function (interleaved floats). Returns null when playing, else the
    /// reason it could not start.
    /// </summary>
    public string? Play(Func<int, int, Func<float[], int, int, int>> source)
    {
        lock (_lock)
        {
            StopOutput();
            MMDevice? device = null;
            WasapiOut? output = null;
            try
            {
                device = AudioDevice.Find(deviceId, deviceName);
                if (device is null) return "Sound card not found";
                var format = device.AudioClient.MixFormat;
                if (SamplePlayback.Problem(format) is { } problem)
                {
                    device.Dispose();
                    return problem;
                }

                output = new WasapiOut(device, AudioClientShareMode.Shared, true, 100);
                output.Init(new SampleProvider(source(format.SampleRate, format.Channels), format));
                output.PlaybackStopped += OnStopped;
                output.Play();
                _device = device;
                _output = output;
                return null;
            }
            catch (Exception e)
            {
                Release(output, device);
                return e.Message;
            }
        }
    }

    public void Stop()
    {
        lock (_lock) StopOutput();
    }

    public void Dispose() => Stop();

    private void StopOutput()
    {
        var output = _output;
        var device = _device;
        _output = null;
        _device = null;
        Release(output, device);
    }

    private void Release(WasapiOut? output, MMDevice? device)
    {
        if (output is not null)
        {
            output.PlaybackStopped -= OnStopped;
            try { output.Stop(); output.Dispose(); }
            catch { /* The card is gone: nothing left to stop */ }
        }
        try { device?.Dispose(); }
        catch { }
    }

    private void OnStopped(object? sender, StoppedEventArgs e)
    {
        if (e.Exception is null) return;
        lock (_lock)
        {
            if (!ReferenceEquals(sender, _output)) return;
            StopOutput();
        }
        Failed?.Invoke(e.Exception.Message);
    }
}
