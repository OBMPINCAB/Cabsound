using System.Text;
using CabSound.Core;
using Xunit;

namespace CabSound.Core.Tests;

public class VpxGameDataTests
{
    private static byte[] Record(string tag, byte[] body) =>
        [.. BitConverter.GetBytes(4 + body.Length), .. Encoding.ASCII.GetBytes(tag), .. body];

    private static byte[] Code(string script)
    {
        var b = Encoding.Latin1.GetBytes(script);
        return [.. BitConverter.GetBytes(4), .. Encoding.ASCII.GetBytes("CODE"), .. BitConverter.GetBytes(b.Length), .. b];
    }

    [Fact]
    public void ReadScript_FindsCodeRecordBetweenOthers()
    {
        byte[] data = [.. Record("LEFT", [1, 2, 3, 4]), .. Code("Option Explicit\r\nSub X : End Sub"), .. Record("SSND", [5, 0, 0, 0]), .. Record("ENDB", [])];

        Assert.Equal("Option Explicit\r\nSub X : End Sub", VpxGameData.ReadScript(data));
    }

    [Fact]
    public void ReadScript_KeepsBytesAsLatin1()
    {
        byte[] data = [.. Code("' café"), .. Record("ENDB", [])];
        Assert.Equal("' café", VpxGameData.ReadScript(data));
    }

    [Fact]
    public void ReadScript_NoCode_ReturnsNull()
    {
        byte[] data = [.. Record("LEFT", [1, 2, 3, 4]), .. Record("ENDB", [])];
        Assert.Null(VpxGameData.ReadScript(data));
    }

    [Fact]
    public void ReadScript_Truncated_ReturnsNull()
    {
        byte[] data = [.. Record("LEFT", [1, 2, 3, 4])];
        Assert.Null(VpxGameData.ReadScript(data[..^2]));
    }
}
