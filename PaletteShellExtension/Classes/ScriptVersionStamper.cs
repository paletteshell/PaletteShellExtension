using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace PaletteShellExtension.Classes;

/// <summary>
/// Backfills a <c>[ScriptVersion('1.0.0')]</c> attribute into .ps1 scripts that don't declare
/// one, so every script PaletteShell manages carries a version rather than relying on the
/// parser's in-memory default. Runs opportunistically during the folder scan.
/// </summary>
/// <remarks>
/// This mutates the user's own files, so every operation is conservative and reversible-by-default:
/// <list type="bullet">
/// <item>It only ever <em>adds</em> the attribute, and only when none is present (idempotent).</item>
/// <item>It skips a script with no top-level <c>param(...)</c> block - there's no safe, valid place
/// to attach a bare attribute in that case, and a wrong insertion could break the script.</item>
/// <item>It preserves the file's existing newline style and UTF-8 BOM (or lack of one), and skips
/// UTF-16 files entirely rather than risk re-encoding them.</item>
/// <item>The write is atomic (temp file + move), and any failure is swallowed - a scan must never
/// fail, or leave a half-written script, over a best-effort version stamp.</item>
/// </list>
/// </remarks>
internal static partial class ScriptVersionStamper
{
    private const string DefaultVersion = "1.0.0";

    /// <summary>Adds <c>[ScriptVersion('1.0.0')]</c> to the script at <paramref name="ps1Path"/> if
    /// it declares no version. Returns true only when the file was actually rewritten.</summary>
    public static bool TryStamp(string ps1Path)
    {
        try
        {
            var bytes = File.ReadAllBytes(ps1Path);

            // A UTF-16 BOM means re-encoding as UTF-8 (what we'd write) would corrupt the file, and
            // the rest of PaletteShell reads scripts as UTF-8 anyway - leave these well alone.
            if (HasUtf16Bom(bytes))
            {
                return false;
            }

            var hasBom = HasUtf8Bom(bytes);
            var bomLength = hasBom ? 3 : 0;
            var content = Encoding.UTF8.GetString(bytes, bomLength, bytes.Length - bomLength);

            if (HasScriptVersion(content))
            {
                return false;
            }

            // Attach the attribute to the top-level param block - the one position that's both a
            // valid PowerShell attribute target and where the parser (and every scaffolded script)
            // already puts the [Script*] attributes. No param block: nothing safe to attach to.
            var param = ParamBlockRegex().Match(content);
            if (!param.Success)
            {
                return false;
            }

            var newline = content.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
            var insertion = $"[ScriptVersion('{DefaultVersion}')]{newline}";
            var stamped = content.Insert(param.Index, insertion);

            WriteAtomic(ps1Path, stamped, hasBom);
            return true;
        }
        catch (Exception ex)
        {
            // Locked file, permissions, a torn read - none of it is worth failing the scan over.
            Log.Warn($"Couldn't stamp ScriptVersion into '{ps1Path}': {ex.Message}");
            return false;
        }
    }

    private static bool HasUtf8Bom(byte[] b) =>
        b.Length >= 3 && b[0] == 0xEF && b[1] == 0xBB && b[2] == 0xBF;

    private static bool HasUtf16Bom(byte[] b) =>
        b.Length >= 2 && ((b[0] == 0xFF && b[1] == 0xFE) || (b[0] == 0xFE && b[1] == 0xFF));

    private static bool HasScriptVersion(string content) => ScriptVersionRegex().IsMatch(content);

    private static void WriteAtomic(string path, string content, bool withBom)
    {
        var encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: withBom);
        var tempPath = path + ".psver.tmp";
        File.WriteAllText(tempPath, content, encoding);
        File.Move(tempPath, path, overwrite: true);
    }

    // A [ScriptVersion ...] attribute anywhere - conservative: if the token appears at all we treat
    // the script as already versioned and leave it untouched, even if it's only in a comment.
    [GeneratedRegex(@"\[\s*ScriptVersion\b", RegexOptions.IgnoreCase)]
    private static partial Regex ScriptVersionRegex();

    // A top-level param block at the start of a line, allowing inline attributes (e.g.
    // "[CmdletBinding()] param("). Anchoring to line start keeps this from matching a stray
    // "param(" buried in a comment or string. The match starts at column 0 so the stamp is
    // inserted on its own line immediately above.
    [GeneratedRegex(@"(?im)^[ \t]*(?:\[[^\]\r\n]*\][ \t]*)*param[ \t]*\(")]
    private static partial Regex ParamBlockRegex();
}
