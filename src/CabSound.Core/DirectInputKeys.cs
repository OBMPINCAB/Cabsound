namespace CabSound.Core;

/// <summary>VPX stores keys as DirectInput scan codes; Windows and Popper use virtual-key codes.</summary>
public static class DirectInputKeys
{
    private static readonly Dictionary<int, int> Map = Build();

    public static int? ToVirtualKey(int dik) => Map.TryGetValue(dik, out var vk) ? vk : null;

    private static Dictionary<int, int> Build()
    {
        var m = new Dictionary<int, int>
        {
            [1] = 0x1B, [12] = 0xBD, [13] = 0xBB, [14] = 0x08, [15] = 0x09, [28] = 0x0D,
            [29] = 0xA2, [42] = 0xA0, [51] = 0xBC, [52] = 0xBE, [53] = 0xBF, [54] = 0xA1,
            [56] = 0xA4, [57] = 0x20, [58] = 0x14, [87] = 0x7A, [88] = 0x7B,
            [71] = 0x67, [72] = 0x68, [73] = 0x69, [75] = 0x64, [76] = 0x65, [77] = 0x66,
            [79] = 0x61, [80] = 0x62, [81] = 0x63, [82] = 0x60,
            [156] = 0x0D, [157] = 0xA3, [184] = 0xA5, [199] = 0x24, [200] = 0x26, [201] = 0x21,
            [203] = 0x25, [205] = 0x27, [207] = 0x23, [208] = 0x28, [209] = 0x22,
            [210] = 0x2D, [211] = 0x2E, [219] = 0x5B, [220] = 0x5C,
        };
        void Row(int startDik, string letters) { for (var i = 0; i < letters.Length; i++) m[startDik + i] = letters[i]; }
        Row(2, "1234567890");
        Row(16, "QWERTYUIOP");
        Row(30, "ASDFGHJKL");
        Row(44, "ZXCVBNM");
        for (var i = 0; i < 10; i++) m[59 + i] = 0x70 + i; // F1-F10
        return m;
    }
}
