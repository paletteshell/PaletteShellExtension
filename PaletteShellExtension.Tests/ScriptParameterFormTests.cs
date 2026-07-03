using System.Text.Json.Nodes;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using PaletteShellExtension.Classes;
using PaletteShellExtension.Forms;
using Xunit;

namespace PaletteShellExtension.Tests;

public class ScriptParameterFormTests
{
    private const string ScriptPath = @"C:\scripts\test.ps1";

    private static ScriptManifest ManifestWithRequiredName(string? label = null) => new()
    {
        Title = "Test",
        Parameters = [new ScriptParameter { Name = "Name", Type = "string", Required = true, Label = label }]
    };

    // ----- GetMissingRequiredFields (pure logic, no script execution) -----------------------

    [Fact]
    public void GetMissingRequiredFields_EmptyValue_IsReportedMissing()
    {
        var manifest = ManifestWithRequiredName();
        var values = JsonNode.Parse("""{"Name":""}""")!.AsObject();

        var missing = ScriptParameterForm.GetMissingRequiredFields(manifest, values);

        Assert.Equal(["Name"], missing);
    }

    [Fact]
    public void GetMissingRequiredFields_WhitespaceOnlyValue_IsReportedMissing()
    {
        var manifest = ManifestWithRequiredName();
        var values = JsonNode.Parse("""{"Name":"   "}""")!.AsObject();

        var missing = ScriptParameterForm.GetMissingRequiredFields(manifest, values);

        Assert.Equal(["Name"], missing);
    }

    [Fact]
    public void GetMissingRequiredFields_UsesLabelWhenPresent()
    {
        var manifest = ManifestWithRequiredName(label: "Full Name");
        var values = JsonNode.Parse("""{"Name":""}""")!.AsObject();

        var missing = ScriptParameterForm.GetMissingRequiredFields(manifest, values);

        Assert.Equal(["Full Name"], missing);
    }

    [Fact]
    public void GetMissingRequiredFields_FilledRequiredField_IsNotReported()
    {
        var manifest = ManifestWithRequiredName();
        var values = JsonNode.Parse("""{"Name":"Alice"}""")!.AsObject();

        var missing = ScriptParameterForm.GetMissingRequiredFields(manifest, values);

        Assert.Empty(missing);
    }

    [Fact]
    public void GetMissingRequiredFields_OptionalFieldEmpty_IsNotReported()
    {
        var manifest = new ScriptManifest
        {
            Title = "Test",
            Parameters = [new ScriptParameter { Name = "Name", Type = "string", Required = null }]
        };
        var values = JsonNode.Parse("""{"Name":""}""")!.AsObject();

        var missing = ScriptParameterForm.GetMissingRequiredFields(manifest, values);

        Assert.Empty(missing);
    }

    // ----- SubmitForm early-exit paths (don't reach script execution) -----------------------

    [Fact]
    public void SubmitForm_MissingRequiredField_KeepsFormOpenWithToast()
    {
        var form = new ScriptParameterForm(ScriptPath, ManifestWithRequiredName());

        var result = form.SubmitForm("""{"Name":""}""", """{"verb":"run"}""");

        Assert.Equal(CommandResultKind.ShowToast, result.Kind);
        var toastArgs = Assert.IsType<ToastArgs>(result.Args);
        Assert.Contains("Name", toastArgs.Message);
        Assert.NotNull(toastArgs.Result);
        Assert.Equal(CommandResultKind.KeepOpen, toastArgs.Result!.Kind);
    }

    [Fact]
    public void SubmitForm_Cancel_Dismisses()
    {
        var form = new ScriptParameterForm(ScriptPath, ManifestWithRequiredName());

        var result = form.SubmitForm("""{"Name":""}""", """{"verb":"cancel"}""");

        Assert.Equal(CommandResultKind.Dismiss, result.Kind);
    }
}
