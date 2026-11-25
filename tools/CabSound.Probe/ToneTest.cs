using CabSound.Platform;
using CabSound.Core;
using NAudio.CoreAudioApi;
using NAudio.Wave;

/// <summary>
/// Bass routing test: plays a sine on one card output pair of the default device, through
/// Equalizer APO (shared mode). Usage: tone backbox|front|rear [seconds=8] [levelDb=-30] [hz=40]
/// </summary>
static class ToneTest
{
    public static int Run(string[] args)
    {
        if (args is ["noise", ..]) return Noise(args);

        // "clicks [seconds=600] [levelDb=-20]": counted clicks per exciter channel (see ClickTest)
        if (args is ["clicks", ..])
        {
            var secs = args.Length > 1 ? double.Parse(args[1]) : 600;
            var db = args.Length > 2 ? double.Parse(args[2]) : -20;
            return Play(f => new ClickTest(f.SampleRate, f.Channels, db).Read, $"clicks at {db} dBFS", secs);
        }

        // "voice <seconds> <file.wav>@<ch> ...": spoken clips, each on its own channel, taking turns
        if (args is ["voice", var s, .. var clipArgs])
        {
            var clips = clipArgs.Select(a => (Samples: ReadMonoWav(a[..a.LastIndexOf('@')]), Channel: int.Parse(a[(a.LastIndexOf('@') + 1)..]))).ToList();
            var names = string.Join(", ", clipArgs.Select(a => $"{Path.GetFileNameWithoutExtension(a[..a.LastIndexOf('@')])} on ch {a[(a.LastIndexOf('@') + 1)..]}"));
            return Play(f => new ClipSequence(f.Channels, clips, f.SampleRate * 6 / 10).Read, names, double.Parse(s));
        }

        // "tone ch <0-7>": one channel only (0 FL, 1 FR, 4 RL, 5 RR, 6 SL, 7 SR)
        int[]? channels = args is ["tone", "ch", var n, ..] && int.TryParse(n, out var c) ? [c] : null;
        if (channels is not null) args = ["tone", $"ch{channels[0]}", .. args[3..]];
        else if (args.Length < 2 || Target(args[1]) is not ToneTarget t)
        {
            Console.WriteLine("Usage: tone backbox|front|rear [seconds=8] [levelDb=-30] [hz=40]   or   tone ch <0-7> [seconds] [levelDb] [hz]");
            return 1;
        }
        else channels = TestTone.ChannelsFor(t);
        var seconds = args.Length > 2 ? double.Parse(args[2]) : 8;
        var levelDb = args.Length > 3 ? double.Parse(args[3]) : -30;
        var hz = args.Length > 4 ? double.Parse(args[4]) : 40;

        return Play(format =>
        {
            // hz 0 = chime, with its own sound per channel so channels can't be confused
            var tone = hz == 0
                ? ChimeFor(channels[0], format, channels, levelDb)
                : new TestTone(format.SampleRate, format.Channels, channels, hz, levelDb);
            return tone.Read;
        }, $"{hz} Hz at {levelDb} dBFS on {string.Join("+", channels)}", seconds);
    }

    // Opens the default device (must be 7.1 float, through Equalizer APO) and plays the source
    static int Play(Func<WaveFormat, Func<float[], int, int, int>> source, string what, double seconds)
    {
        var device = new MMDeviceEnumerator().GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
        var format = device.AudioClient.MixFormat;
        var mask = SamplePlayback.ChannelMask(format);
        Console.WriteLine($"Device: {device.FriendlyName}, {format.Channels} ch, {format.SampleRate} Hz, " +
                          $"{format.Encoding} {format.BitsPerSample} bit, mask 0x{mask:X}");
        if (SamplePlayback.Problem(format) is { } problem) { Console.WriteLine($"{problem} - stopping."); return 1; }

        using var output = new WasapiOut(device, AudioClientShareMode.Shared, true, 100);
        output.Init(new SampleProvider(source(format), format));
        Console.WriteLine($"Playing {what} for {seconds} s...");
        output.Play();
        Thread.Sleep(TimeSpan.FromSeconds(seconds));
        output.Stop();
        Console.WriteLine("Done.");
        return 0;
    }

    // ch 4: high fast pings, 5: medium, 6: bell once a second, 7: low slow; others: bell
    static TestTone ChimeFor(int channel, WaveFormat f, int[] channels, double levelDb) => channel switch
    {
        4 => TestTone.Chime(f.SampleRate, f.Channels, channels, levelDb, 1320, 3),
        5 => TestTone.Chime(f.SampleRate, f.Channels, channels, levelDb, 660, 2),
        7 => TestTone.Chime(f.SampleRate, f.Channels, channels, levelDb, 440, 0.5),
        _ => TestTone.Chime(f.SampleRate, f.Channels, channels, levelDb, 880, 1),
    };

    // 16-bit mono WAV at the device rate (48 kHz) → float samples
    static float[] ReadMonoWav(string path)
    {
        using var reader = new WaveFileReader(path);
        if (reader.WaveFormat.Channels != 1 || reader.WaveFormat.BitsPerSample != 16)
            throw new InvalidDataException($"{path}: need 16-bit mono");
        var bytes = new byte[reader.Length];
        var read = reader.Read(bytes, 0, bytes.Length);
        return Enumerable.Range(0, read / 2).Select(i => BitConverter.ToInt16(bytes, i * 2) / 32768f).ToArray();
    }

    static ToneTarget? Target(string s) => s.ToLowerInvariant() switch
    {
        "backbox" => ToneTarget.Backbox,
        "front" => ToneTarget.FrontExciters,
        "rear" => ToneTarget.RearExciters,
        _ => null
    };


    // "noise level|low backbox|front|rear [seconds=5]": the calibration wizard's noise on one group of the USB card
    static int Noise(string[] args)
    {
        if (args.Length < 3 || args[1] is not ("level" or "low") || Target(args[2]) is not ToneTarget target)
        {
            Console.WriteLine("Usage: noise level|low backbox|front|rear [seconds=5]");
            return 1;
        }
        var seconds = args.Length > 3 ? double.Parse(args[3]) : 5;
        var channels = TestTone.ChannelsFor(target);

        using var player = new NoisePlayer(null, "USB");
        player.Failed += reason => Console.WriteLine($"Playback stopped: {reason}");
        var problem = player.Play((rate, count) =>
            (args[1] == "low" ? BandNoise.Low(rate, count, channels) : BandNoise.Level(rate, count, channels)).Read);
        if (problem is not null) { Console.WriteLine($"Can't play: {problem}"); return 1; }

        Console.WriteLine($"Playing {args[1]} noise ({BandNoise.LevelDb} dBFS) on {args[2]} for {seconds} s...");
        Thread.Sleep(TimeSpan.FromSeconds(seconds));
        player.Stop();
        Console.WriteLine("Done.");
        return 0;
    }
}
