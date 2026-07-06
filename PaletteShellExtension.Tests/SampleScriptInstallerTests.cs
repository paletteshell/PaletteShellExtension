using System;
using System.IO;
using PaletteShellExtension.Classes;
using Xunit;

namespace PaletteShellExtension.Tests;

public class InstalledSampleScriptsTests
{
    private static string CreateTempRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), $"pstest-samples-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        return root;
    }

    [Fact]
    public void Record_ThenTryGet_RoundTrips()
    {
        var root = CreateTempRoot();
        try
        {
            var store = new InstalledSampleScripts(root);

            Assert.False(store.TryGet("Foo.ps1", out _));

            store.Record("Foo.ps1", "1.0.0", "hash1");

            Assert.True(store.TryGet("Foo.ps1", out var record));
            Assert.Equal("1.0.0", record!.Version);
            Assert.Equal("hash1", record.ContentHash);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Record_PersistsAcrossInstances()
    {
        var root = CreateTempRoot();
        try
        {
            new InstalledSampleScripts(root).Record("Foo.ps1", "1.0.0", "hash1");

            var reloaded = new InstalledSampleScripts(root);

            Assert.True(reloaded.TryGet("Foo.ps1", out var record));
            Assert.Equal("1.0.0", record!.Version);
            Assert.Equal("hash1", record.ContentHash);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void CorruptStoreFile_LoadsAsEmpty()
    {
        var root = CreateTempRoot();
        try
        {
            File.WriteAllText(Path.Combine(root, "sample-scripts.json"), "not json");

            var store = new InstalledSampleScripts(root);

            Assert.False(store.TryGet("Foo.ps1", out _));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}

public class SampleScriptSyncTests
{
    [Fact]
    public void ShouldOverwrite_FreshInstall_ReturnsTrue()
    {
        Assert.True(SampleScriptSync.ShouldOverwrite(targetExists: false, record: null, onDiskHash: null, shippedVersion: "1.0.0"));
    }

    [Fact]
    public void ShouldOverwrite_UntrackedExistingFile_ReturnsFalse()
    {
        Assert.False(SampleScriptSync.ShouldOverwrite(targetExists: true, record: null, onDiskHash: "somehash", shippedVersion: "2.0.0"));
    }

    [Fact]
    public void ShouldOverwrite_PreviouslyInstalledNowMissing_ReturnsFalse()
    {
        // The file was installed before (we have a record) but is gone now - the user (or the
        // Script Manager's "Remove") deleted it on purpose. Don't fight that by recreating it.
        var record = new InstalledSampleScript("1.0.0", "originalHash");

        Assert.False(SampleScriptSync.ShouldOverwrite(targetExists: false, record, onDiskHash: null, shippedVersion: "1.1.0"));
    }

    [Fact]
    public void ShouldOverwrite_UserModifiedFile_ReturnsFalse()
    {
        var record = new InstalledSampleScript("1.0.0", "originalHash");

        Assert.False(SampleScriptSync.ShouldOverwrite(targetExists: true, record, onDiskHash: "editedHash", shippedVersion: "2.0.0"));
    }

    [Fact]
    public void ShouldOverwrite_UnmodifiedWithNewerVersion_ReturnsTrue()
    {
        var record = new InstalledSampleScript("1.0.0", "originalHash");

        Assert.True(SampleScriptSync.ShouldOverwrite(targetExists: true, record, onDiskHash: "originalHash", shippedVersion: "1.1.0"));
    }

    [Fact]
    public void ShouldOverwrite_UnmodifiedWithSameVersion_ReturnsFalse()
    {
        var record = new InstalledSampleScript("1.0.0", "originalHash");

        Assert.False(SampleScriptSync.ShouldOverwrite(targetExists: true, record, onDiskHash: "originalHash", shippedVersion: "1.0.0"));
    }

    [Fact]
    public void ShouldOverwrite_UnmodifiedWithOlderVersion_ReturnsFalse()
    {
        var record = new InstalledSampleScript("2.0.0", "originalHash");

        Assert.False(SampleScriptSync.ShouldOverwrite(targetExists: true, record, onDiskHash: "originalHash", shippedVersion: "1.0.0"));
    }

    [Theory]
    [InlineData("1.0.0", "1.0.0", false)]
    [InlineData("1.1.0", "1.0.0", true)]
    [InlineData("1.0.0", "1.1.0", false)]
    [InlineData(null, "1.0.0", false)]
    [InlineData("1.0.0", null, true)]
    public void IsNewerVersion_ComparesCorrectly(string? candidate, string? baseline, bool expected)
    {
        Assert.Equal(expected, SampleScriptSync.IsNewerVersion(candidate, baseline));
    }
}
