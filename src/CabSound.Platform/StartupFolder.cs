using CabSound.Core;

namespace CabSound.Platform;

/// <summary>Carries out StartupPlan steps in the user's Startup folder.</summary>
public static class StartupFolder
{
    public static string Path => Environment.GetFolderPath(Environment.SpecialFolder.Startup);

    public static List<string> Files(string dir) =>
        Directory.Exists(dir) ? Directory.GetFiles(dir).Select(f => System.IO.Path.GetFileName(f)).ToList() : [];

    public static void Apply(IEnumerable<StartupStep> steps, string parkDir, string cabSoundExe)
    {
        var link = System.IO.Path.Combine(Path, StartupPlan.CabSoundLink);
        foreach (var step in steps)
        {
            switch (step)
            {
                case StartupStep.AddCabSound: CreateShortcut(link, cabSoundExe); break;
                case StartupStep.RemoveCabSound: File.Delete(link); break;
                case StartupStep.ParkPinVol: MovePinVol(Path, parkDir); break;
                case StartupStep.RestorePinVol: MovePinVol(parkDir, Path); break;
            }
        }
    }

    private static void MovePinVol(string from, string to)
    {
        Directory.CreateDirectory(to);
        foreach (var name in Files(from).Where(StartupPlan.IsPinVol))
            File.Move(System.IO.Path.Combine(from, name), System.IO.Path.Combine(to, name), overwrite: true);
    }

    private static void CreateShortcut(string linkPath, string target)
    {
        var shellType = Type.GetTypeFromProgID("WScript.Shell") ?? throw new InvalidOperationException("WScript.Shell not available");
        dynamic shell = Activator.CreateInstance(shellType)!;
        var shortcut = shell.CreateShortcut(linkPath);
        shortcut.TargetPath = target;
        shortcut.WorkingDirectory = System.IO.Path.GetDirectoryName(target);
        shortcut.Save();
    }
}
