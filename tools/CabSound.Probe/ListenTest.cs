using NAudio.CoreAudioApi;
using NAudio.Wave;

/// <summary>
/// Measures what programs send to each channel of the default device (WASAPI loopback), so channel
/// questions don't depend on ears. Usage: listen [seconds=30] [floorDb=-60]
/// Prints the peak level per channel for every 100 ms that is louder than the floor, then a summary.
/// </summary>
static class ListenTest
{
    private static readonly string[] Names = ["L", "R", "C", "LFE", "RL", "RR", "SL", "SR"];

    public static int Run(string[] args)
    {
        var seconds = args.Length > 1 ? double.Parse(args[1]) : 30;
        var floorDb = args.Length > 2 ? double.Parse(args[2]) : -60;

        var device = new MMDeviceEnumerator().GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
        using var capture = new WasapiLoopbackCapture(device);
        var format = capture.WaveFormat;
        Console.WriteLine($"Device: {device.FriendlyName}, {format.Channels} ch, {format.SampleRate} Hz, {format.BitsPerSample} bit");
        if (format.Channels != 8 || format.BitsPerSample != 32) { Console.WriteLine("Not 8 channels of 32-bit float - stopping."); return 1; }

        var window = format.SampleRate / 10;           // 100 ms
        var peak = new float[8];
        var energy = new double[8];                    // summed over the loud windows only
        int inWindow = 0, windows = 0, loud = 0;
        var start = DateTime.Now;
        Console.WriteLine("time     " + string.Join("", Names.Select(n => $"{n,7}")));

        capture.DataAvailable += (_, e) =>
        {
            for (var i = 0; i + 32 <= e.BytesRecorded; i += 32)
            {
                for (var c = 0; c < 8; c++) peak[c] = Math.Max(peak[c], Math.Abs(BitConverter.ToSingle(e.Buffer, i + c * 4)));
                if (++inWindow < window) continue;

                windows++;
                if (Db(peak.Max()) >= floorDb)
                {
                    loud++;
                    for (var c = 0; c < 8; c++) energy[c] += peak[c] * peak[c];
                    Console.WriteLine($"{(DateTime.Now - start).TotalSeconds,7:0.0}  " + string.Join("", peak.Select(p => $"{Level(p),7}")));
                }
                Array.Clear(peak);
                inWindow = 0;
            }
        };

        capture.StartRecording();
        Thread.Sleep(TimeSpan.FromSeconds(seconds));
        capture.StopRecording();

        Console.WriteLine($"Loud windows: {loud} of {windows}. Average peak level over the loud windows (dBFS):");
        Console.WriteLine("         " + string.Join("", Names.Select(n => $"{n,7}")));
        Console.WriteLine("         " + string.Join("", energy.Select(en => $"{(loud == 0 ? "-" : Level((float)Math.Sqrt(en / loud))),7}")));
        return 0;
    }

    private static double Db(float amplitude) => amplitude <= 0 ? -200 : 20 * Math.Log10(amplitude);

    private static string Level(float amplitude) => Db(amplitude) < -90 ? "-" : Db(amplitude).ToString("0");
}
