using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using PaletteShellExtension.Classes;
using PaletteShellExtension.Forms;
using System;
using System.Collections.Generic;

namespace PaletteShellExtension.Pages;

internal sealed partial class ScriptParameterFormPage : ContentPage
{
    // Kept so the input form can be rebuilt fresh (see GetContent).
    private readonly string _scriptPath;
    private readonly ScriptManifest _manifest;
    private readonly ScriptExecutionPlan _plan;

    private ScriptParameterForm _form;
    private readonly MarkdownContent _markdown = new();
    private IContent[] _content;

    // Timestamp of the last run activity (start / result / finish). Used by GetContent to tell a
    // just-finished run's render (which should keep showing the result) apart from the page being
    // reopened later (which should return to a fresh form).
    private long _lastRunActivityTick;

    public ScriptParameterFormPage(
        string scriptPath,
        ScriptManifest manifest,
        ScriptExecutionPlan plan)
    {
        _scriptPath = scriptPath;
        _manifest = manifest;
        _plan = plan;

        _form = CreateForm();
        _content = [_form];

        Title = manifest.Title ?? "Run Script";
        Name = "Enter Parameters";
        Icon = new(manifest.IconGlyph ?? "");
        Id = $"ScriptParams_{System.IO.Path.GetFileNameWithoutExtension(scriptPath)}";
    }

    private ScriptParameterForm CreateForm()
        => new(_scriptPath, _manifest, _plan, ShowContent, BeginRun, EndRun);

    // Called by the form the moment a run starts, so the user gets immediate feedback instead
    // of a frozen form: swap to a "Running…" panel and turn on the page's loading spinner while
    // the script executes on a background thread. The rendered result replaces it once ready.
    private void BeginRun()
    {
        _markdown.Body = $"### ⏳ Running {Title}…\n\nThis can take a few seconds.";
        _content = [_markdown];
        _lastRunActivityTick = Environment.TickCount64;
        IsLoading = true;
        RaiseItemsChanged();
    }

    // Called when the run finishes; clears the loading spinner. The result content itself is
    // set by ShowMarkdown just before this.
    private void EndRun()
    {
        _lastRunActivityTick = Environment.TickCount64;
        IsLoading = false;
        RaiseItemsChanged();
    }

    // Called by the form to replace the input form with rendered script output or an actionable
    // failure card.
    private void ShowContent(IContent content)
    {
        _content = [content];
        _lastRunActivityTick = Environment.TickCount64;
        RaiseItemsChanged();
    }

    public override IContent[] GetContent()
    {
        // This page instance is reused across navigations. If it's still showing a previous run's
        // result and this fetch isn't part of that just-finished run, the page is being reopened —
        // so rebuild a fresh input form. (A brand-new form is built because re-displaying the
        // already-submitted one leaves the page stuck loading.)
        //
        // The grace window covers the content fetches that immediately follow a run; a reopen
        // happens well after. The palette only fetches content on navigation or when we raise
        // ItemsChanged — it doesn't poll — so this won't wipe a result while you're viewing it.
        const long GraceMs = 1000;
        if (_content.Length == 1
            && ReferenceEquals(_content[0], _markdown)
            && Environment.TickCount64 - _lastRunActivityTick > GraceMs)
        {
            _form = CreateForm();
            _content = [_form];
        }

        return _content;
    }
}
