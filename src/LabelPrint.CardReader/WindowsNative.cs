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

    [DllImport("winscard.dll", EntryPoint = "SCardConnectW", CharSet = CharSet.Unicode)]
    public static extern int SCardConnect(
        IntPtr context,
        string reader,
        uint shareMode,
        uint preferredProtocols,
        out IntPtr card,
        out uint activeProtocol);

    [DllImport("winscard.dll", EntryPoint = "SCardDisconnect")]
    public static extern int SCardDisconnect(IntPtr card, uint disposition);

    [DllImport("winscard.dll", EntryPoint = "SCardTransmit")]
    public static extern int SCardTransmit(
        IntPtr card,
        ref ScardIoRequest sendPci,
        byte[] sendBuffer,
        int sendLength,
        IntPtr recvPci,
        byte[] recvBuffer,
        ref int recvLength);

    public static IntPtr LoadOrThrow(string path)
    {
        var handle = LoadLibrary(path);
        if (handle != IntPtr.Zero)
            return handle;

        throw new CardReaderException(
            $"LoadLibrary 無法載入 {path}：{new Win32Exception(Marshal.GetLastWin32Error()).Message}");
    }
}

[StructLayout(LayoutKind.Sequential)]
internal struct ScardIoRequest
{
    public uint Protocol;
    public int PciLength;
}

[SupportedOSPlatform("windows")]
internal static class PcscReaders
{
    public const int NoReaders = unchecked((int)0x8010002E);

    public const int NoSmartCard = unchecked((int)0x8010000C);

    private const uint ShareShared = 2;

    private const uint ProtocolT0 = 1;

    private const uint UnpowerCard = 2;

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

    /// <summary>ACR38U path: select reader "ACS CCID USB Reader 0" and read 5 bytes at 248 (APDU FF B0 00 F8 05).</summary>
    public static CardIdRead ReadAcr38()
    {
        var readers = List();
        if (!readers.Any(name => name.Contains(CardCodec.Acr38ReaderName, StringComparison.OrdinalIgnoreCase)))
            throw new CardReaderException("reader-open-failed");

        var rc = WindowsNative.SCardEstablishContext(0, IntPtr.Zero, IntPtr.Zero, out var context);
        if (rc != 0)
            throw new CardReaderException("reader-open-failed", rc);

        var card = IntPtr.Zero;
        try
        {
            rc = WindowsNative.SCardConnect(context, CardCodec.Acr38ReaderName, ShareShared, ProtocolT0, out card, out var protocol);
            if (rc == NoSmartCard)
                return CardIdRead.Absent();
            if (rc != 0)
                throw new CardReaderException("reader-open-failed", rc);

            var send = new byte[] { 0xFF, 0xB0, 0x00, 248, 5 };
            var recv = new byte[16];
            var recvLength = recv.Length;
            var pci = new ScardIoRequest { Protocol = protocol, PciLength = Marshal.SizeOf<ScardIoRequest>() };
            rc = WindowsNative.SCardTransmit(card, ref pci, send, send.Length, IntPtr.Zero, recv, ref recvLength);
            if (rc == NoSmartCard)
                return CardIdRead.Absent();
            if (rc != 0 || recvLength < 2)
                return CardIdRead.Absent();

            var dataLength = recvLength >= 2 && recv[recvLength - 2] == 0x90 && recv[recvLength - 1] == 0x00
                ? recvLength - 2
                : recvLength;
            if (dataLength < CardCodec.CardIdLength)
                return CardIdRead.Absent();

            var hex = CardCodec.FormatCardId(recv.AsSpan(0, CardCodec.CardIdLength));
            return CardCodec.IsBlankCardId(hex) ? CardIdRead.Absent() : CardIdRead.Present(hex);
        }
        finally
        {
            if (card != IntPtr.Zero)
                _ = WindowsNative.SCardDisconnect(card, UnpowerCard);
            _ = WindowsNative.SCardReleaseContext(context);
        }
    }
}
