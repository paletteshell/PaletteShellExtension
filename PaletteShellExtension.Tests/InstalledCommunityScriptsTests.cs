using System;
using System.IO;
using PaletteShellExtension.Classes;
using Xunit;

namespace PaletteShellExtension.Tests;

public class InstalledCommunityScriptsTests
{
    private static string CreateTempRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), $"pstest-installed-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        return root;
    }

    [Fact]
    public void Record_ThenTryGetInstalled_RoundTrips()
    {
        var root = CreateTempRoot();
        try
        {
            var store = new InstalledCommunityScripts(root);
            var localPath = Path.Combine(root, "Foo.ps1");

            Assert.False(store.TryGetInstalled(localPath, out _));

            store.Record(localPath, "Clipboard/Foo.ps1", "sha1");

            Assert.True(store.TryGetInstalled(localPath, out var record));
            Assert.Equal("Clipboard/Foo.ps1", record!.SourcePath);
            Assert.Equal("sha1", record.Sha);
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
            var localPath = Path.Combine(root, "Foo.ps1");
            new InstalledCommunityScripts(root).Record(localPath, "Clipboard/Foo.ps1", "sha1");

            var reloaded = new InstalledCommunityScripts(root);

            Assert.True(reloaded.TryGetInstalled(localPath, out var record));
            Assert.Equal("sha1", record!.Sha);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Record_WithVersion_RoundTripsAcrossInstances()
    {
        var root = CreateTempRoot();
        try
        {
            var localPath = Path.Combine(root, "Foo.ps1");
            new InstalledCommunityScripts(root).Record(localPath, "Clipboard/Foo.ps1", "sha1", "1.2.0");

            var reloaded = new InstalledCommunityScripts(root);

            Assert.True(reloaded.TryGetInstalled(localPath, out var record));
            Assert.Equal("1.2.0", record!.Version);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Remove_ClearsTheRecord()
    {
        var root = CreateTempRoot();
        try
        {
            var store = new InstalledCommunityScripts(root);
            var localPath = Path.Combine(root, "Foo.ps1");
            store.Record(localPath, "Clipboard/Foo.ps1", "sha1");

            store.Remove(localPath);

            Assert.False(store.TryGetInstalled(localPath, out _));
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
            File.WriteAllText(Path.Combine(root, "community-installed.json"), "not json");

            var store = new InstalledCommunityScripts(root);

            Assert.False(store.TryGetInstalled(Path.Combine(root, "Foo.ps1"), out _));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
