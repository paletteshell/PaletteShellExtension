namespace PaletteShellExtension.Classes;

internal static class PowerShellQuoting
{
    /// <summary>
    /// Wraps a value as a PowerShell single-quoted string literal, doubling embedded
    /// quotes so it can't break out of the string or inject commands.
    /// </summary>
    public static string SingleQuote(string? value)
        => "'" + (value ?? "").Replace("'", "''") + "'";
}
