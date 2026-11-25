using System;
using System.Threading.Tasks;
using System.Windows;
using CabSound.Core;
using CabSound.Platform;

namespace CabSound.App;

/// <summary>
/// Manages the speaker check session: start checks, test mode on/off, playing sample sources
/// on the sound card, volume slider, and jack swap toggle.
/// UI thread only; the engine is reached through <see cref="Workers.Run"/>. One session at a time.
/// </summary>
public sealed class CalibrationSession(Workers workers, EngineState state) : IDisposable
{
    public const string TableStarted = "A table started, so the window was closed. The table has its normal levels.";

    private readonly NoisePlayer _player = new(state.SoundCardId, state.SoundCardName);
    private (GroupLevels Trial, int? ExciterCut, int? BackboxCut)? _sampleTrial;   // what PlaySamples last wrote
    private bool _started, _ended, _seenCalibrating;

    /// <summary>A speaker check window is open.</summary>
    public static bool IsOpen { get; private set; }

    /// <summary>The window must close: a table started, or the test sound could not play.</summary>
    public event Action<string>? Stopped;

    /// <summary>null = speaker check can open now; else the sentence to show.</summary>
    public static string? CannotStart(EngineState state)
    {
        if (IsOpen) return "A speaker check window is already open.";
        if (state.TableRunning) return "Close the table first: this needs the cabinet quiet.";
        if (state.SoundCardId is null) return "Sound card not found (Sound tab).";
        using var device = AudioDevice.Find(state.SoundCardId, state.SoundCardName);
        if (device is null) return "Sound card not found (Sound tab).";
        var status = EqApoCheck.Status(device.ID);
        return status == EqApoStatus.Active ? null : EqApoHealth.Warning(status) ?? "Equalizer APO isn't active on the sound card.";
    }

    /// <summary>Switches test mode on (on the engine thread, waited for). null = started; else why not.</summary>
    public string? Start()
    {
        if (IsOpen) return "A speaker check window is already open.";
        var begun = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        workers.Run((e, _) => begun.TrySetResult(e.BeginCalibration()));
        if (!begun.Task.Wait(TimeSpan.FromSeconds(5)))
        {
            workers.Run((e, _) => e.EndCalibration());
            return "CabSound is busy. Try again in a moment.";
        }
        if (!begun.Task.Result) return "Close the table first: this needs the cabinet quiet.";

        _player.Failed += OnPlaybackFailed;
        _started = IsOpen = true;
        return null;
    }

    /// <summary>
    /// Speaker / bass check: writes these trial levels and cuts and starts this sample source; null = silence.
    /// Always restarts. source gets the card's sample rate and channel count.
    /// </summary>
    public void PlaySamples(GroupLevels trial, Func<int, int, Func<float[], int, int, int>>? source,
        int? exciterCut = null, int? backboxCut = null)
    {
        if (!Active) return;
        _player.Stop();
        if (source is null) return;

        // Only when they change: every write makes Equalizer APO reload the file
        var next = (trial, exciterCut, backboxCut);
        if (next != _sampleTrial) workers.Run((e, _) => e.SetCalibrationTrial(trial, exciterCut, backboxCut));
        _sampleTrial = next;
        if (_player.Play(source) is { } problem) Fail($"The test sound can't play: {problem}.");
    }

    /// <summary>Stores a finished speaker check's one-line verdict (shown on the Sound tab).</summary>
    public void SaveSpeakerCheck(string summary)
    {
        if (Active) workers.Run((e, now) => e.SaveSpeakerCheck(summary, Today, now));
    }

    /// <summary>The speaker check's fix: the jack swap setting, with an undo point; CabSound.txt updates immediately.</summary>
    public void SetJackSwap(bool on)
    {
        if (Active) workers.Run((e, now) => { e.SaveSettingsUndoPoint(); e.SetSound(e.State.Sound with { JacksSwapped = on }, now); });
    }

    /// <summary>The window's volume slider (Windows volume, 0..100).</summary>
    public void SetVolume(int percent)
    {
        if (Active) workers.Run((e, _) => e.SetCalibrationVolume(percent));
    }

    /// <summary>Stops the test sound and leaves test mode.</summary>
    public void End()
    {
        if (!Active) return;
        _ended = true;
        IsOpen = false;
        _player.Stop();
        workers.Run((e, _) => e.EndCalibration());
    }

    /// <summary>Every engine state, from the Sound tab: test mode ended by the engine = a table started.</summary>
    public void OnState(EngineState s)
    {
        if (!Active) return;
        if (s.Calibrating) _seenCalibrating = true;
        else if (_seenCalibrating) Fail(TableStarted);
    }

    public void Dispose()
    {
        End();
        _player.Failed -= OnPlaybackFailed;
        _player.Dispose();
    }

    private bool Active => _started && !_ended;

    private static DateOnly Today => DateOnly.FromDateTime(DateTime.Now);

    private void OnPlaybackFailed(string reason) =>
        Application.Current?.Dispatcher.BeginInvoke(() => Fail($"The test sound stopped: {reason}."));

    private void Fail(string reason)
    {
        if (!Active) return;
        Log.Write($"Speaker check stopped: {reason}");
        End();
        Stopped?.Invoke(reason);
    }
}
