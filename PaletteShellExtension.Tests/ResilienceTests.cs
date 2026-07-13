using System;
using System.IO;
using System.Linq;
using PaletteShellExtension.Classes;
using Xunit;

namespace PaletteShellExtension.Tests;

// Regression tests for the Partner Center crash class: failures on the COM activation
// path (bad scripts folder, corrupt settings file) must degrade, never throw.
public class ResilienceTests
{
    // ----- Corrupt settings quarantine ----------------------------------------------------

    [Fact]
    public void QuarantineCorruptSettingsFile_InvalidJson_MovesFileToBad()
    {
        var path = Path.Combine(Path.GetTempPath(), $"palette-test-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, "{ not valid json !!");
        try
        {
            PaletteShellSettingsManager.QuarantineCorruptSettingsFile(path);

            Assert.False(File.Exists(path));
            Assert.True(File.Exists(path + ".bad"));
        }
        finally
        {
            File.Delete(path);
            File.Delete(path + ".bad");
        }
    }

    [Fact]
    public void QuarantineCorruptSettingsFile_ValidJson_LeavesFileAlone()
    {
        var path = Path.Combine(Path.GetTempPath(), $"palette-test-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, """{"defaultHost":"auto"}""");
        try
        {
            PaletteShellSettingsManager.QuarantineCorruptSettingsFile(path);

            Assert.True(File.Exists(path));
            Assert.False(File.Exists(path + ".bad"));
        }
        finally
        {
            File.Delete(path);
            File.Delete(path + ".bad");
        }
    }

    [Fact]
    public void QuarantineCorruptSettingsFile_MissingOrNullPath_IsANoOp()
    {
        PaletteShellSettingsManager.QuarantineCorruptSettingsFile(null);
        PaletteShellSettingsManager.QuarantineCorruptSettingsFile("");
        PaletteShellSettingsManager.QuarantineCorruptSettingsFile(
            Path.Combine(Path.GetTempPath(), $"palette-missing-{Guid.NewGuid():N}.json"));
    }

    // ----- Inaccessible scripts folder ----------------------------------------------------

    [Fact]
    public void Page_ConfiguredFolderOnMissingDrive_DegradesToSetupItemInsteadOfThrowing()
    {
        // A drive letter that doesn't exist on this machine stands in for the unplugged
        // USB stick / offline share from the field reports.
        var used = DriveInfo.GetDrives()
            .Select(d => char.ToUpperInvariant(d.Name[0]))
            .ToHashSet();
        var letter = "ZYXWVUTSRQPONMLKJIHGFEDCBA".First(c => !used.Contains(c));
        var badFolder = $@"{letter}:\PaletteShellTests\{Guid.NewGuid():N}";

        var original = PaletteShellSettingsManager.Instance.ScriptsFolder;
        try
        {
            PaletteShellSettingsManager.Instance.ScriptsFolder = badFolder;

            // Would previously throw DirectoryNotFoundException out of the constructor —
            // i.e. crash the extension process during COM activation.
            var page = new PaletteShellExtensionPage();
            var items = page.GetItems();

            var item = Assert.Single(items);
            Assert.Equal("Choose scripts folder", item.Title);
            Assert.Contains(badFolder, item.Subtitle, StringComparison.Ordinal);
        }
        finally
        {
            PaletteShellSettingsManager.Instance.ScriptsFolder = original;
        }
    }
}
