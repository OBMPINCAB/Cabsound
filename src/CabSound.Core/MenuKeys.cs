namespace CabSound.Core;

/// <summary>
/// The cabinet keys the on-screen menu uses, read from VPX's own settings so they always match.
/// While the menu is open these keys are held back from VPX; Exit and everything else pass through.
/// </summary>
public sealed record MenuKeys(int LeftFlipper, int RightFlipper, int LeftMagna, int RightMagna, int Start, int Pause)
{
    public static MenuKeys? FromVpx(IEnumerable<ReservedInput> vpx)
    {
        var keys = vpx.Where(r => r.Input.Kind == InputKind.Key)
                      .GroupBy(r => r.Name).ToDictionary(g => g.Key, g => g.First().Input.Code);
        int? K(string name) => keys.TryGetValue(name, out var vk) ? vk : null;

        return K("LFlipKey") is int lf && K("RFlipKey") is int rf && K("LMagnaSave") is int lm
            && K("RMagnaSave") is int rm && K("StartGameKey") is int st && K("PauseKey") is int p
            ? new MenuKeys(lf, rf, lm, rm, st, p)
            : null;
    }

    public MenuCommand? ToCommand(int vk) =>
        vk == LeftFlipper ? MenuCommand.Up :
        vk == RightFlipper ? MenuCommand.Down :
        vk == LeftMagna ? MenuCommand.Decrease :
        vk == RightMagna ? MenuCommand.Increase :
        vk == Start ? MenuCommand.Close :
        null;

    public bool ShouldHoldBack(int vk) => ToCommand(vk) is not null;
}
