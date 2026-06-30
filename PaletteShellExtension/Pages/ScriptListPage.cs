using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using PaletteShellExtension.Classes;
using PaletteShellExtension.Commands;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace PaletteShellExtension.Pages;

/// <summary>
/// Runs a script and turns its captured stdout into a searchable list of results,
/// where each line (or each element of a JSON array) becomes a selectable item.
/// Used when a script declares <c>[ScriptOutput('List')]</c>. This is what lets a
/// script act as a search/pick provider — e.g. "list my Git branches → pick one → copy".
///
/// Two flavors, chosen by whether the script declares a parameter:
///   • No parameter → the script runs once and the palette's search box filters the
///     results locally (e.g. a fixed list of branches).
///   • One parameter → the page is a live provider: the palette's search text is passed
///     to the script as that parameter and the results refresh as you type (e.g. type a
///     folder path → branches for that repo). Only the first parameter is used.
///
/// Parsing convention for stdout:
///   • If it parses as a JSON array, each element becomes an item:
///       - a string                → title and copy value are the string
///       - an object               → fields map to the item (see <see cref="ResultItem"/>)
///   • Otherwise it is treated as newline-delimited text and each non-empty line becomes
///     an item whose title and copy value are that line.
/// Picking an item copies its value; items that carry a URL also get an "Open" command.
/// </summary>
internal sealed partial class ScriptListPage : DynamicListPage
{
    private readonly string _scriptPath;
    private readonly ScriptManifest _manifest;
    private readonly string _host;
    private readonly string? _cwd;
    private readonly Dictionary<string, string> _env;

    // When set, the page feeds the palette's search text to the script as this parameter
    // and re-runs on change; when null the script runs once and search filters locally.
    private readonly string? _queryParam;

    private IListItem[] _items = [];
    private bool _started;

    // Guards against stale async runs clobbering newer ones as the user types.
    private int _queryVersion;

    // Debounce so we don't launch a process on every keystroke.
    private const int DebounceMs = 300;

    public ScriptListPage(
        string scriptPath,
        ScriptManifest manifest,
        string? host = null,
        string? cwd = null,
        Dictionary<string, string>? env = null)
    {
        _scriptPath = scriptPath;
        _manifest = manifest;
        _host = host ?? manifest.Host ?? "pwsh";
        _cwd = cwd;
        _env = env ?? new(StringComparer.OrdinalIgnoreCase);
        _queryParam = manifest.Parameters.FirstOrDefault()?.Name;

        Title = manifest.Title ?? Path.GetFileNameWithoutExtension(scriptPath);
        Name = "Run";
        Icon = new(manifest.IconGlyph ?? "");
        Id = $"ScriptList_{Path.GetFileNameWithoutExtension(scriptPath)}";
        ShowDetails = false;

        if (_queryParam is not null)
        {
            // Surface what to type — the parameter's help/label is the best hint we have.
            PlaceholderText = manifest.Parameters[0].Label ?? _queryParam;
        }

        IsLoading = true;
    }

    public override IListItem[] GetItems()
    {
        // Kick off the first run the first time the host asks for items.
        if (!_started)
        {
            _started = true;
            Run(SearchText);
        }

        // For the static (no-parameter) case the search box filters locally; for the
        // dynamic case the script already produced query-specific results, so show them.
        return _queryParam is null ? Filter(_items, SearchText) : _items;
    }

    public override void UpdateSearchText(string oldSearch, string newSearch)
    {
        if (_queryParam is null)
        {
            // Local filtering only — just re-emit the (already loaded) items.
            RaiseItemsChanged();
            return;
        }

        // Dynamic provider: re-run the script with the new query, debounced.
        var version = ++_queryVersion;
        IsLoading = true;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(DebounceMs);
                if (version != _queryVersion)
                {
                    return; // Superseded by a newer keystroke.
                }
                Run(newSearch, version);
            }
            catch (Exception)
            {
                // Never let a background failure crash the host.
            }
        });
    }

    /// <summary>Runs the script (with the query as its argument, if dynamic) and rebuilds the
    /// item list. <paramref name="version"/>, when given, is checked after the run so a slow
    /// query can't overwrite the results of a newer one.</summary>
    private void Run(string? query, int? version = null)
    {
        if (version is null)
        {
            _ = Task.Run(() => Execute(query, version));
        }
        else
        {
            Execute(query, version);
        }
    }

    private void Execute(string? query, int? version)
    {
        try
        {
            var args = BuildArgs(query);

            var timeout = _manifest.TimeoutMs is > 0 ? _manifest.TimeoutMs!.Value : 30000;

            // Elevated scripts can't have their output captured, so List mode always
            // runs unelevated — there'd be nothing to list otherwise.
            var result = ScriptRunner.RunScriptAndWait(
                scriptPath: _scriptPath,
                args: args,
                host: _host,
                cwd: _cwd,
                env: _env,
                requiresAdmin: false,
                timeoutMs: timeout);

            if (version is not null && version != _queryVersion)
            {
                return; // A newer query finished after we started; discard this one.
            }

            _items = BuildItems(result);
        }
        catch (Exception ex)
        {
            _items = [Message($"Error running script: {ex.Message}")];
        }
        finally
        {
            if (version is null || version == _queryVersion)
            {
                IsLoading = false;
                RaiseItemsChanged();
            }
        }
    }

    /// <summary>Builds the command-line argument that passes the search text to the script's
    /// query parameter. Empty text is omitted so the script's own default applies.</summary>
    private string BuildArgs(string? query)
    {
        if (_queryParam is null || string.IsNullOrEmpty(query))
        {
            return "";
        }

        // Single-quote the literal so paths and spaces reach the script intact.
        return $"-{_queryParam} '{query.Replace("'", "''")}'";
    }

    private static IListItem[] Filter(IListItem[] items, string? search)
    {
        if (string.IsNullOrWhiteSpace(search))
        {
            return items;
        }

        return [.. items.Where(i =>
            (i.Title?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false)
            || (i.Subtitle?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false))];
    }

    private static IListItem[] BuildItems(ScriptRunner.ScriptResult? result)
    {
        if (result is null)
            return [Message("Failed to start script.")];

        if (result.TimedOut)
            return [Message("Script timed out.")];

        if (result.ExitCode != 0)
            return [Message(ScriptRunner.DescribeFailure(result))];

        var output = result.StandardOutput;
        if (string.IsNullOrWhiteSpace(output))
            return [Message("Script produced no results.")];

        var rows = Parse(output);
        if (rows.Count == 0)
            return [Message("Script produced no results.")];

        return [.. rows.Select(ToListItem)];
    }

    /// <summary>Parses stdout into result rows — a JSON array if it looks like one,
    /// otherwise newline-delimited text.</summary>
    private static List<ResultItem> Parse(string output)
    {
        var trimmed = output.Trim();

        // Only attempt JSON when it actually looks like an array; this keeps plain
        // text that merely starts with a bracket out of the JSON path.
        if (trimmed.StartsWith('['))
        {
            try
            {
                using var doc = JsonDocument.Parse(trimmed);
                if (doc.RootElement.ValueKind == JsonValueKind.Array)
                {
                    var items = new List<ResultItem>();
                    foreach (var element in doc.RootElement.EnumerateArray())
                    {
                        var item = FromJson(element);
                        if (item is not null)
                            items.Add(item);
                    }
                    return items;
                }
            }
            catch (JsonException)
            {
                // Not valid JSON after all — fall through to line parsing.
            }
        }

        return trimmed
            .Replace("\r\n", "\n")
            .Split('\n')
            .Select(line => line.TrimEnd())
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Select(line => new ResultItem { Title = line, Value = line })
            .ToList();
    }

    private static ResultItem? FromJson(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                var title = ReadString(element, "title", "name", "label", "text");
                var value = ReadString(element, "value", "copy") ?? title;
                if (string.IsNullOrEmpty(title))
                    title = value;
                if (string.IsNullOrEmpty(title))
                    return null;

                return new ResultItem
                {
                    Title = title!,
                    Subtitle = ReadString(element, "subtitle", "description", "detail"),
                    Value = value ?? title!,
                    Url = ReadString(element, "url", "link"),
                    Icon = ReadString(element, "icon")
                };

            case JsonValueKind.Null or JsonValueKind.Undefined:
                return null;

            // Strings, numbers, and booleans become a plain item.
            default:
                var raw = element.ValueKind == JsonValueKind.String
                    ? element.GetString() ?? ""
                    : element.GetRawText();
                return string.IsNullOrWhiteSpace(raw)
                    ? null
                    : new ResultItem { Title = raw, Value = raw };
        }
    }

    private static string? ReadString(JsonElement obj, params string[] names)
    {
        foreach (var prop in obj.EnumerateObject())
        {
            foreach (var name in names)
            {
                if (string.Equals(prop.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    return prop.Value.ValueKind == JsonValueKind.String
                        ? prop.Value.GetString()
                        : prop.Value.GetRawText();
                }
            }
        }
        return null;
    }

    private static ListItem ToListItem(ResultItem row)
    {
        var url = !string.IsNullOrWhiteSpace(row.Url)
            ? row.Url
            : LooksLikeUrl(row.Value) ? row.Value : null;

        var item = new ListItem(new CopyValueCommand(row.Value))
        {
            Title = row.Title,
            Subtitle = row.Subtitle ?? "",
            Icon = string.IsNullOrWhiteSpace(row.Icon) ? null : new IconInfo(row.Icon),
        };

        if (url is not null)
        {
            item.MoreCommands = [new CommandContextItem(new OpenLinkCommand("Open", url, ""))];
        }

        return item;
    }

    private static bool LooksLikeUrl(string? value)
        => value is not null
           && (value.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
               || value.StartsWith("https://", StringComparison.OrdinalIgnoreCase));

    private static ListItem Message(string text)
        => new(new NoOpCommand()) { Title = text };

    /// <summary>A single parsed result before it becomes a list item.</summary>
    private sealed class ResultItem
    {
        public string Title { get; set; } = "";
        public string? Subtitle { get; set; }
        public string Value { get; set; } = "";
        public string? Url { get; set; }
        public string? Icon { get; set; }
    }
}
