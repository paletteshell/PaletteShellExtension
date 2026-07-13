using System.IO;
using PaletteShellExtension.Classes;
using PaletteShellExtension.Forms;
using Xunit;

namespace PaletteShellExtension.Tests;

public class NewScriptWizardFormTests
{
    [Fact]
    public void SubmitForm_Create_WritesScriptWithAllOptions_AndParserReadsItBack()
    {
        var root = Directory.CreateTempSubdirectory("pswizard-").FullName;
        try
        {
            var form = new NewScriptWizardForm(root);
            var inputs = """
                {
                  "name": "MyTestScript",
                  "description": "Does a thing",
                  "group": "My Group",
                  "icon": "🎯",
                  "output": "Result",
                  "host": "pwsh",
                  "timeout": "45000",
                  "elevate": "true",
                  "confirm": "Are you sure?",
                  "open": "false"
                }
                """;

            form.SubmitForm(inputs, """{"verb":"create"}""");

            var path = Path.Combine(root, "MyTestScript.ps1");
            Assert.True(File.Exists(path));

            var manifest = PowerShellScriptParser.TryParseManifest(path);
            Assert.NotNull(manifest);
            Assert.Equal("MyTestScript", manifest!.Title);
            Assert.Equal("Does a thing", manifest.Description);
            Assert.Equal("My Group", manifest.Group);
            Assert.Equal("🎯", manifest.IconGlyph);
            Assert.Equal("Result", manifest.Output);
            Assert.Equal("pwsh", manifest.Host);
            Assert.Equal(45000, manifest.TimeoutMs);
            Assert.True(manifest.RequiresAdmin);
            Assert.Equal("Are you sure?", manifest.ConfirmMessage);
            Assert.Equal(AppVersion.Current.ToString(), manifest.MinVersion);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void SubmitForm_OmittedOptions_OmitHostAndTimeoutForRuntimeDefaults()
    {
        var root = Directory.CreateTempSubdirectory("pswizard-").FullName;
        try
        {
            var form = new NewScriptWizardForm(root);
            form.SubmitForm("""{"name":"Defaults","open":"false"}""", """{"verb":"create"}""");

            var manifest = PowerShellScriptParser.TryParseManifest(Path.Combine(root, "Defaults.ps1"));

            Assert.NotNull(manifest);
            Assert.Equal("General", manifest!.Group);
            Assert.Equal("None", manifest.Output);
            // Host/timeout are left unset in the file itself, so the script picks up
            // whatever the global default settings are at run time.
            Assert.Null(manifest.Host);
            Assert.Null(manifest.TimeoutMs);
            Assert.Null(manifest.RequiresAdmin);
            Assert.Null(manifest.ConfirmMessage);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void SubmitForm_TimeoutBelowMinimum_ClampsToMinimum()
    {
        var root = Directory.CreateTempSubdirectory("pswizard-").FullName;
        try
        {
            var form = new NewScriptWizardForm(root);
            form.SubmitForm("""{"name":"BadTimeout","timeout":"10","open":"false"}""", """{"verb":"create"}""");

            var manifest = PowerShellScriptParser.TryParseManifest(Path.Combine(root, "BadTimeout.ps1"));

            Assert.Equal(1000, manifest!.TimeoutMs);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void SubmitForm_HostDefault_OmitsScriptHostAttribute()
    {
        var root = Directory.CreateTempSubdirectory("pswizard-").FullName;
        try
        {
            var form = new NewScriptWizardForm(root);
            form.SubmitForm("""{"name":"DefaultHost","host":"default","open":"false"}""", """{"verb":"create"}""");

            var manifest = PowerShellScriptParser.TryParseManifest(Path.Combine(root, "DefaultHost.ps1"));

            Assert.Null(manifest!.Host);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void SubmitForm_Cancel_DoesNotCreateFile()
    {
        var root = Directory.CreateTempSubdirectory("pswizard-").FullName;
        try
        {
            var form = new NewScriptWizardForm(root);

            form.SubmitForm("{}", """{"verb":"cancel"}""");

            Assert.False(Directory.Exists(root) && Directory.GetFiles(root).Length > 0);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }
}
