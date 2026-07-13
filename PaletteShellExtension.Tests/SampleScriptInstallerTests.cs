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

            store.Record("Foo.ps1", "hash1");

            Assert.True(store.TryGet("Foo.ps1", out var record));
            Assert.Equal("hash1", record!.ContentHash);
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
            new InstalledSampleScripts(root).Record("Foo.ps1", "hash1");

            var reloaded = new InstalledSampleScripts(root);

            Assert.True(reloaded.TryGet("Foo.ps1", out var record));
            Assert.Equal("hash1", record!.ContentHash);
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
            Assert.Null(store.SyncedAppVersion);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void FreshStore_HasNoSyncedVersion()
    {
        var root = CreateTempRoot();
        try
        {
            Assert.Null(new InstalledSampleScripts(root).SyncedAppVersion);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void MarkSynced_PersistsVersionAndRecordsAcrossInstances()
    {
        var root = CreateTempRoot();
        try
        {
            var store = new InstalledSampleScripts(root);
            store.Record("Foo.ps1", "hash1", persist: false);
            store.MarkSynced("1.2.3.0");

            var reloaded = new InstalledSampleScripts(root);

            Assert.Equal("1.2.3.0", reloaded.SyncedAppVersion);
            Assert.True(reloaded.TryGet("Foo.ps1", out var record));
            Assert.Equal("hash1", record!.ContentHash);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void LegacyFlatFormat_LoadsRecordsWithoutSyncedVersion()
    {
        var root = CreateTempRoot();
        try
        {
            // The pre-stamp format was the per-script map at the top level.
            File.WriteAllText(
                Path.Combine(root, "sample-scripts.json"),
                """{ "Foo.ps1": { "hash": "hash1" } }""");

            var store = new InstalledSampleScripts(root);

            Assert.Null(store.SyncedAppVersion);
            Assert.True(store.TryGet("Foo.ps1", out var record));
            Assert.Equal("hash1", record!.ContentHash);
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
        Assert.True(SampleScriptSync.ShouldOverwrite(targetExists: false, record: null, onDiskHash: null, shippedHash: "hash1"));
    }

    [Fact]
    public void ShouldOverwrite_UntrackedExistingFile_ReturnsFalse()
    {
        Assert.False(SampleScriptSync.ShouldOverwrite(targetExists: true, record: null, onDiskHash: "somehash", shippedHash: "newHash"));
    }

    [Fact]
    public void ShouldOverwrite_PreviouslyInstalledNowMissing_ReturnsFalse()
    {
        // The file was installed before (we have a record) but is gone now - the user (or the
        // Script Manager's "Remove") deleted it on purpose. Don't fight that by recreating it.
        var record = new InstalledSampleScript("originalHash");

        Assert.False(SampleScriptSync.ShouldOverwrite(targetExists: false, record, onDiskHash: null, shippedHash: "newHash"));
    }

    [Fact]
    public void ShouldOverwrite_UserModifiedFile_ReturnsFalse()
    {
        var record = new InstalledSampleScript("originalHash");

        Assert.False(SampleScriptSync.ShouldOverwrite(targetExists: true, record, onDiskHash: "editedHash", shippedHash: "newHash"));
    }

    [Fact]
    public void ShouldOverwrite_UnmodifiedWithChangedShippedContent_ReturnsTrue()
    {
        var record = new InstalledSampleScript("originalHash");

        Assert.True(SampleScriptSync.ShouldOverwrite(targetExists: true, record, onDiskHash: "originalHash", shippedHash: "newHash"));
    }

    [Fact]
    public void ShouldOverwrite_UnmodifiedWithUnchangedShippedContent_ReturnsFalse()
    {
        var record = new InstalledSampleScript("originalHash");

        Assert.False(SampleScriptSync.ShouldOverwrite(targetExists: true, record, onDiskHash: "originalHash", shippedHash: "originalHash"));
    }
}
