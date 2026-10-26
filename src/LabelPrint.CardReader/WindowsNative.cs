using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace LabelPrint.CardReader;

/// <summary>
/// Native entry points. Integer widths match VB6: Integer is 16-bit, Long is 32-bit, and Declare is StdCall.
/// TODO(on-machine): confirm with dumpbin /exports and a probe before any card write. A wrong width crashes the process.
/// </summary>
[SupportedOSPlatform("windows")]
internal static class WindowsNative
{
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true, EntryPoint = "LoadLibraryW")]
    public static extern IntPtr LoadLibrary(string lpFileName);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool FreeLibrary(IntPtr hModule);

    [DllImport("kernel32.dll", CharSet = CharSet.Ansi, SetLastError = true, ExactSpelling = true)]
    public static extern IntPtr GetProcAddress(IntPtr hModule, string procName);

    [DllImport("winscard.dll", EntryPoint = "SCardEstablishContext", CharSet = CharSet.Unicode)]
    public static extern int SCardEstablishContext(uint scope, IntPtr reserved1, IntPtr reserved2, out IntPtr context);

    [DllImport("winscard.dll", EntryPoint = "SCardListReadersW", CharSet = CharSet.Unicode)]
    public static extern int SCardListReaders(IntPtr context, string? groups, char[]? readers, ref int pcchReaders);

    [DllImport("winscard.dll", EntryPoint = "SCardReleaseContext")]
    public static extern int SCardReleaseContext(IntPtr context);

    public static IntPtr LoadOrThrow(string path)
    {
        var handle = LoadLibrary(path);
        if (handle != IntPtr.Zero)
            return handle;

        throw new CardReaderException(
            $"LoadLibrary 無法載入 {path}：{new Win32Exception(Marshal.GetLastWin32Error()).Message}");
    }
}

[SupportedOSPlatform("windows")]
internal static class PcscReaders
{
    public const int NoReaders = unchecked((int)0x8010002E);

    public static IReadOnlyList<string> List()
    {
        var rc = WindowsNative.SCardEstablishContext(0, IntPtr.Zero, IntPtr.Zero, out var context);
        if (rc != 0)
            throw new CardReaderException($"SCardEstablishContext 失敗，代碼 0x{rc:X8}。", rc);

        try
        {
            var length = 0;
            rc = WindowsNative.SCardListReaders(context, null, null, ref length);
            if (rc == NoReaders)
                return [];
            if (rc != 0)
                throw new CardReaderException($"SCardListReaders 失敗，代碼 0x{rc:X8}。", rc);

            var buffer = new char[Math.Max(length, 1)];
            rc = WindowsNative.SCardListReaders(context, null, buffer, ref length);
            if (rc == NoReaders)
                return [];
            if (rc != 0)
                throw new CardReaderException($"SCardListReaders 失敗，代碼 0x{rc:X8}。", rc);

            return new string(buffer).Split('\0', StringSplitOptions.RemoveEmptyEntries);
        }
        finally
        {
            _ = WindowsNative.SCardReleaseContext(context);
        }
    }
}
