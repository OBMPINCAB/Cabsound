using System;
using CabSound.Core;
using CabSound.Platform;
using NAudio.CoreAudioApi;

namespace CabSound.App;

/// <summary>
/// The engine's real outside world. Runs on the engine thread. Failures are logged and shown as a
/// tray warning, never thrown (the next change simply tries again).
/// </summary>
public sealed class EngineOutput(string? deviceId, string deviceName, Popup popup, Action<string> warn) : IEngineOutput, IDisposable
{
    private MMDevice? _device;
    private bool _programCheckFailed;

    public void UseSoundCard(string id, string name)
    {
        ReleaseDevice();
        (deviceId, deviceName) = (id, name);
        Log.Write($"Sound card changed to '{name}'");
    }

    public void SetVolume(int percent) => Try("Set volume", () =>
    {
        // Found here (engine thread) and found again after a failure, e.g. card unplugged and back
        _device ??= AudioDevice.Find(deviceId, deviceName) ?? throw new InvalidOperationException($"sound card '{deviceName}' not found");
        try { AudioDevice.SetVolume(_device, percent); }
        catch { ReleaseDevice(); throw; }
    });

    /// <summary>
    /// Puts pinball programs that Windows' volume mixer has turned down or muted on the card back to
    /// 100% (logged, tray message). Quiet when the card isn't there: SetVolume reports that.
    /// </summary>
    public void FixProgramVolumes()
    {
        try
        {
            _device ??= AudioDevice.Find(deviceId, deviceName);
            if (_device is null) return;
            var fixedPrograms = ProgramVolumeFix.Run(_device);
            _programCheckFailed = false;
            if (fixedPrograms.Count == 0) return;
            var message = ProgramVolumes.Message(fixedPrograms);
            Log.Write(message);
            warn(message);
        }
        catch (Exception e)
        {
            ReleaseDevice();
            if (!_programCheckFailed) Log.Write($"Volume mixer check failed: {e.Message}");
            _programCheckFailed = true; // runs every 2 s: one log line, not a flood
        }
    }

    public void WriteInclude(string text) => Try("Write CabSound.txt", () => SafeFileWriter.Write(Paths.Include, text));

    public void SaveData(CabSoundData data) => Try("Save CabSound.json", () => CabSoundDataFile.Save(Paths.DataFile, data));

    public void ShowPopup(PopupInfo info) => popup.Dispatcher.BeginInvoke(() => popup.ShowInfo(info));

    public void Dispose() => ReleaseDevice();

    private void ReleaseDevice()
    {
        var d = _device;
        _device = null;
        try { d?.Dispose(); } catch { }
    }

    private void Try(string what, Action action)
    {
        try { action(); }
        catch (Exception e)
        {
            Log.Write($"{what} failed: {e.Message}");
            warn($"{what} failed: {e.Message}");
        }
    }
}
