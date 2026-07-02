using System;
using System.IO;
using System.Runtime.InteropServices;

namespace PaletteShellExtension.Classes;

/// <summary>
/// Sends a file to the Windows Recycle Bin via <c>SHFileOperation</c> so a "Delete" from the
/// palette stays recoverable, matching what File Explorer does. Direct P/Invoke keeps this
/// dependency-free and trim-safe (no reflection), unlike Microsoft.VisualBasic's FileSystem.
/// </summary>
internal static class RecycleBin
{
    private const uint FO_DELETE = 0x0003;
    private const ushort FOF_ALLOWUNDO = 0x0040;      // Route through the Recycle Bin.
    private const ushort FOF_NOCONFIRMATION = 0x0010; // We already confirm in the palette.
    private const ushort FOF_SILENT = 0x0004;
    private const ushort FOF_NOERRORUI = 0x0400;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct SHFILEOPSTRUCT
    {
        public IntPtr hwnd;
        public uint wFunc;
        public string pFrom;
        public string? pTo;
        public ushort fFlags;
        public int fAnyOperationsAborted;
        public IntPtr hNameMappings;
        public string? lpszProgressTitle;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHFileOperation(ref SHFILEOPSTRUCT lpFileOp);

    /// <summary>Moves <paramref name="path"/> to the Recycle Bin. Returns true on success.</summary>
    public static bool TryDelete(string path)
    {
        if (!File.Exists(path))
        {
            return false;
        }

        // pFrom is a double-null-terminated list of paths.
        var op = new SHFILEOPSTRUCT
        {
            wFunc = FO_DELETE,
            pFrom = path + '\0' + '\0',
            fFlags = FOF_ALLOWUNDO | FOF_NOCONFIRMATION | FOF_SILENT | FOF_NOERRORUI,
        };

        return SHFileOperation(ref op) == 0 && op.fAnyOperationsAborted == 0;
    }
}
