using PaletteShellExtension.Classes;
using Xunit;

namespace PaletteShellExtension.Tests;

public class ScriptOutputHandlerTests
{
    [Theory]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData("   ", null)]
    [InlineData("https://example.com", "https://example.com")]
    [InlineData("\r\nC:\\Temp\r\nignored", "C:\\Temp")]
    [InlineData("\"C:\\Path With Spaces\"", "C:\\Path With Spaces")]
    public void GetOpenTarget_ReturnsFirstNonEmptyLine(string? output, string? expected)
    {
        Assert.Equal(expected, ScriptOutputHandler.GetOpenTarget(output));
    }
}
