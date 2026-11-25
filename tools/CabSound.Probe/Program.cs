using CabSound.Core;
using CabSound.Platform;
using Binding = CabSound.Core.Binding;

// CabSound Probe: checks everything step 3 touches. Changes nothing.
// "tone ..." runs the bass routing test tone instead; "noise ..." the calibration noise (see ToneTest.cs).
if (args is ["tone", ..] or ["clicks", ..] or ["voice", ..] or ["noise", ..]) return ToneTest.Run(args);
// "listen [seconds] [floorDb]": measures what is sent to each channel (see ListenTest.cs)
if (args is ["listen", ..]) return ListenTest.Run(args);
// "device [name]" / "eqapo [name]": name = part of the card's name (default "USB"; a driver can rename the card)
if (args is ["device", ..])
{
    var hint = args.Length > 1 ? args[1] : "USB";
    var d = AudioDevice.Find(null, hint);
    Console.WriteLine(d is null ? $"Sound card '{hint}': NOT FOUND" : $"Found: {d.FriendlyName}\nID: {d.ID}\nDriver: {AudioDevice.Driver(d) ?? "unknown"}");
    return d is null ? 1 : 0;
}
// "word [text]": can Windows speak the speaker check's word? Makes no sound.
if (args is ["word", ..])
{
    var samples = SpokenWord.Render(args.Length > 1 ? args[1] : "here", 48000);
    Console.WriteLine(samples is null ? "Windows speech: NOT AVAILABLE (the speaker check uses clacks only)"
        : $"Windows speech: OK, {samples.Length} samples ({samples.Length / 48000.0:0.00} s), peak {samples.Max(Math.Abs):0.00}");
    return samples is null ? 1 : 0;
}
if (args is ["eqapo", ..])
{
    var hint = args.Length > 1 ? args[1] : "USB";
    var d = AudioDevice.Find(null, hint);
    Console.WriteLine(d is null ? $"Sound card '{hint}': NOT FOUND" : $"Equalizer APO on {d.FriendlyName}: {EqApoCheck.Status(d.ID)}");
    return 0;
}

var logPath = Path.Combine(Environment.CurrentDirectory, "probe-log.txt");
var log = new StreamWriter(logPath, append: false) { AutoFlush = true };
void Say(string s) { var line = $"{DateTime.Now:HH:mm:ss.fff}  {s}"; Console.WriteLine(line); log.WriteLine(line); }

Say("CabSound Probe - changes nothing. Close this window to stop.");

Try("Sound", () => Say($"Sound device: {SystemVolume.DeviceName()}, Windows volume {SystemVolume.Get()}%"));

Try("Joysticks", () =>
{
    var joys = JoystickReader.List();
    if (joys.Count == 0) Say("Joysticks: none found");
    foreach (var (id, name) in joys) Say($"Joystick {id}: {name}");
});

var reserved = new List<ReservedInput>();
MenuKeys? menuKeys = null;
Try("VPX keys", () =>
{
    var ini = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "VPinballX", "VPinballX.ini");
    var vpx = ConflictChecker.FromVpxIni(File.ReadAllText(ini));
    reserved.AddRange(vpx);
    menuKeys = MenuKeys.FromVpx(vpx);
    Say($"VPX: {vpx.Count} keys/buttons in use; menu keys {(menuKeys is null ? "NOT FOUND" : "found")}");
});
Try("Popper keys", () =>
{
    var pop = ConflictChecker.FromPopper(PopperControls.Read());
    reserved.AddRange(pop);
    Say($"Popper: {pop.Count} keys/buttons in use");
});

var twins = new ButtonTwins();
var started = Environment.TickCount64;

string Clash(InputId input)
{
    twins.Learn(input, Environment.TickCount64 - started);
    var all = twins.Expand(input);
    var hits = reserved.Where(r => all.Contains(r.Input)).Select(r => $"{r.Owner} {r.Name}").ToList();
    return hits.Count == 0 ? "FREE (safe to use)" : "IN USE by " + string.Join(", ", hits);
}

string KeyName(int vk) => Enum.IsDefined(typeof(ConsoleKey), vk) ? ((ConsoleKey)vk).ToString() : vk switch
{
    0xA0 => "LeftShift", 0xA1 => "RightShift", 0xA2 => "LeftCtrl", 0xA3 => "RightCtrl",
    0xA4 => "LeftAlt", 0xA5 => "RightAlt", _ => "?"
};

var keyboard = new KeyboardWatcher();
var lastTable = "";

var joystick = new JoystickReader();
joystick.Button += (id, button, down) =>
{
    if (down) Say($"PRESS  joystick {id} button {button,2}  -> {Clash(InputId.Joy(button))}");
};

keyboard.Key += (vk, down, _) =>
{
    if (down) Say($"PRESS  key 0x{vk:X2} {KeyName(vk),-12} -> {Clash(InputId.Key(vk))}");
};
keyboard.Start();

Say("Watching. Press cabinet buttons, launch a table.");

var nextTableCheck = DateTime.MinValue;
while (true)
{
    joystick.Poll();
    if (DateTime.Now >= nextTableCheck)
    {
        nextTableCheck = DateTime.Now.AddSeconds(2);
        Try("Table", () =>
        {
            var key = TableWatcher.CurrentKey();
            if (key != lastTable) { Say($"TABLE  now: {key}"); lastTable = key; }
        });
    }
    Thread.Sleep(20);
}

void Try(string what, Action action)
{
    try { action(); }
    catch (Exception e) { Say($"ERROR ({what}): {e.Message}"); }
}
