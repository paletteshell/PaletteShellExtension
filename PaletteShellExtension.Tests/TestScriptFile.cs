using System;
using System.IO;

namespace PaletteShellExtension.Tests;

/// <summary>Writes a temporary .ps1 file for a test and deletes it on dispose.</summary>
internal sealed class TestScriptFile : IDisposable
{
    public string Path { get; }

    public TestScriptFile(string content)
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"pstest-{Guid.NewGuid():N}.ps1");
        File.WriteAllText(Path, content, new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    public void Dispose()
    {
        try { File.Delete(Path); } catch { /* best-effort cleanup */ }
    }
}
