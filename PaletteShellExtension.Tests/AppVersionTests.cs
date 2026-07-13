using PaletteShellExtension.Classes;
using Xunit;

namespace PaletteShellExtension.Tests;

public class AppVersionTests
{
    [Fact]
    public void IsCompatible_WhenMinVersionNull_ReturnsTrue()
    {
        Assert.True(AppVersion.IsCompatible(null, out var required));
        Assert.Null(required);
    }

    [Fact]
    public void IsCompatible_WhenMinVersionUnparseable_FailsOpen()
    {
        Assert.True(AppVersion.IsCompatible("not-a-version", out var required));
        Assert.Null(required);
    }

    [Fact]
    public void IsCompatible_WhenMinVersionAtOrBelowCurrent_ReturnsTrue()
    {
        Assert.True(AppVersion.IsCompatible(AppVersion.Current.ToString(), out _));
        Assert.True(AppVersion.IsCompatible("0.0.0.0", out _));
    }

    [Fact]
    public void IsCompatible_WhenMinVersionAboveCurrent_ReturnsFalse()
    {
        var future = new System.Version(AppVersion.Current.Major + 1, 0, 0, 0);

        var result = AppVersion.IsCompatible(future.ToString(), out var required);

        Assert.False(result);
        Assert.Equal(future, required);
    }

    [Fact]
    public void IsCompatible_WhenMaxVersionNullOrUnset_ReturnsTrue()
    {
        Assert.True(AppVersion.IsCompatible(null, null, out var required, out var tooNew));
        Assert.Null(required);
        Assert.False(tooNew);
    }

    [Fact]
    public void IsCompatible_WhenMaxVersionUnparseable_FailsOpen()
    {
        Assert.True(AppVersion.IsCompatible(null, "not-a-version", out var required, out var tooNew));
        Assert.Null(required);
        Assert.False(tooNew);
    }

    [Fact]
    public void IsCompatible_WhenMaxVersionAtOrAboveCurrent_ReturnsTrue()
    {
        Assert.True(AppVersion.IsCompatible(null, AppVersion.Current.ToString(), out _, out _));

        var future = new System.Version(AppVersion.Current.Major + 1, 0, 0, 0);
        Assert.True(AppVersion.IsCompatible(null, future.ToString(), out _, out _));
    }

    [Fact]
    public void IsCompatible_WhenMaxVersionBelowCurrent_ReturnsFalseAndFlagsTooNew()
    {
        var past = new System.Version(0, 0, 0, 1);

        var result = AppVersion.IsCompatible(null, past.ToString(), out var required, out var tooNew);

        Assert.False(result);
        Assert.Equal(past, required);
        Assert.True(tooNew);
    }

    [Fact]
    public void IsCompatible_WhenBothViolated_MinimumWins()
    {
        var future = new System.Version(AppVersion.Current.Major + 1, 0, 0, 0);
        var past = new System.Version(0, 0, 0, 1);

        var result = AppVersion.IsCompatible(future.ToString(), past.ToString(), out var required, out var tooNew);

        Assert.False(result);
        Assert.Equal(future, required);
        Assert.False(tooNew);
    }
}
