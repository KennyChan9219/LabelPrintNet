using System.Text;

namespace LabelPrint;

public enum CardType
{
    Sle4442,
    Sle4428
}

/// <summary>Reader model, port, and card family stored in <c>CardReader.config</c>. No secrets.</summary>
public sealed class CardReaderSettings
{
    public string Model { get; set; } = CardReaderModelNames.Rd600P;

    /// <summary>frmMemberCard calls <c>IC_InitComm(101)</c> for RD600P. The comment in that sub says 100; the call uses 101.</summary>
    public int Port { get; set; } = 101;

    public CardType CardType { get; set; } = CardType.Sle4442;

    /// <summary>RD600P calls <c>IC_InitType(icdev, 16)</c> before the hex read.</summary>
    public int? InitType { get; set; } = 16;
}

public static class CardReaderModelNames
{
    public const string Acr30U = "ACR30U";

    public const string Acr38U = "ACR38U";

    public const string Rd600P = "RD600P";

    public static IReadOnlyList<string> Known { get; } = [Acr30U, Acr38U, Rd600P];

    /// <summary>Same rule as <c>UCase$(Left$(file, 6))</c>. Anything else is RD600P.</summary>
    public static string Normalize(string? token)
    {
        var text = (token ?? "").Trim().ToUpperInvariant();
        var six = text.Length >= 6 ? text[..6] : text;
        if (six == Acr30U)
            return Acr30U;
        if (six == Acr38U)
            return Acr38U;
        return Rd600P;
    }
}

/// <summary>Result of the frmMemberCard card-id read. The id is 10 hex characters, not an ASCII member number.</summary>
public sealed class CardIdRead
{
    public bool CardPresent { get; init; }

    public string CardId { get; init; } = "";

    public static CardIdRead Absent() => new();

    public static CardIdRead Present(string cardId) => new()
    {
        CardPresent = true,
        CardId = cardId.Trim()
    };
}

public sealed class CardProbeResult
{
    public bool Success { get; init; }

    public int Code { get; init; }

    public string Message { get; init; } = "";
}

public sealed class CardMemory
{
    public required int Offset { get; init; }

    public required byte[] Bytes { get; init; }

    public string? MemberNumber { get; init; }

    public string ToHex()
    {
        var builder = new System.Text.StringBuilder(Bytes.Length * 3);
        for (var i = 0; i < Bytes.Length; i++)
        {
            if (i > 0)
                builder.Append(i % 16 == 0 ? Environment.NewLine : " ");
            builder.Append(Bytes[i].ToString("X2", System.Globalization.CultureInfo.InvariantCulture));
        }

        return builder.ToString();
    }
}

/// <summary>Pure SLE4428/4442 field encoding. Offsets are configuration, not hardcoded production dumps.</summary>
public static class CardCodec
{
    public const int CardIdOffset = 248;

    public const int CardIdLength = 5;

    public const string BlankCardId = "FFFFFFFFFF";

    public const string Acr38ReaderName = "ACS CCID USB Reader 0";

    public static string FormatCardId(ReadOnlySpan<byte> bytes)
    {
        var take = Math.Min(bytes.Length, CardIdLength);
        var builder = new StringBuilder(take * 2);
        for (var i = 0; i < take; i++)
            builder.Append(bytes[i].ToString("X2", System.Globalization.CultureInfo.InvariantCulture));
        return builder.ToString();
    }

    public static bool IsBlankCardId(string? hex) =>
        string.Equals(hex?.Trim(), BlankCardId, StringComparison.OrdinalIgnoreCase);

    public static int Capacity(CardType type) => type switch
    {
        CardType.Sle4442 => 256,
        CardType.Sle4428 => 1024,
        _ => throw new ArgumentOutOfRangeException(nameof(type))
    };

    public static void ValidateWindow(CardType type, int offset, int length)
    {
        if (offset < 0)
            throw new ArgumentOutOfRangeException(nameof(offset), "卡片偏移不可為負數。");
        if (length <= 0)
            throw new ArgumentOutOfRangeException(nameof(length), "卡片長度必須大於 0。");

        var capacity = Capacity(type);
        if (offset > capacity || length > capacity - offset)
        {
            throw new ArgumentOutOfRangeException(
                nameof(length),
                $"卡片範圍 {offset}+{length} 超出 {type} 主記憶體（{capacity} 位元組）。");
        }
    }

    public static byte[]? ParsePassword(CardType type, string? hex)
    {
        if (string.IsNullOrWhiteSpace(hex))
            return null;

        var bytes = ParseHex(hex);
        var expected = type == CardType.Sle4442 ? 3 : 2;
        if (bytes.Length != expected)
        {
            throw new FormatException(
                $"{type} 密碼必須是 {expected} 位元組（{expected * 2} 個十六進位數字）。");
        }

        return bytes;
    }

    public static byte[] ParseHex(string hex)
    {
        var buffer = new List<byte>(hex.Length / 2);
        var nibble = -1;
        foreach (var character in hex)
        {
            if (character is ' ' or ':' or '-' or '\t')
                continue;

            var digit = HexValue(character);
            if (digit < 0)
                throw new FormatException("密碼十六進位包含非十六進位字元。");

            if (nibble < 0)
            {
                nibble = digit;
            }
            else
            {
                buffer.Add((byte)((nibble << 4) | digit));
                nibble = -1;
            }
        }

        if (nibble >= 0)
            throw new FormatException("密碼十六進位的位數必須是雙數。");

        return buffer.ToArray();
    }

    public static byte[] EncodeMemberNumber(string memberNumber, int length)
    {
        if (length is <= 0 or > 64)
            throw new ArgumentOutOfRangeException(nameof(length));

        var text = memberNumber.Trim();
        if (text.Length == 0)
            throw new ArgumentException("會員編號是空的。", nameof(memberNumber));
        if (text.Length > length)
            throw new ArgumentException($"會員編號長過卡片欄位（{length}）。", nameof(memberNumber));

        foreach (var character in text)
        {
            if (character is < (char)32 or > (char)126)
                throw new ArgumentException("寫入卡片的會員編號必須是可列印的 ASCII。", nameof(memberNumber));
        }

        var bytes = new byte[length];
        Encoding.ASCII.GetBytes(text, bytes);
        return bytes;
    }

    public static string DecodeMemberNumber(ReadOnlySpan<byte> bytes)
    {
        var end = bytes.Length;
        while (end > 0 && bytes[end - 1] is 0 or 32)
            end--;

        var chars = new char[end];
        for (var i = 0; i < end; i++)
        {
            var value = bytes[i];
            chars[i] = value is >= 32 and <= 126 ? (char)value : '?';
        }

        return new string(chars).Trim();
    }

    private static int HexValue(char character) => character switch
    {
        >= '0' and <= '9' => character - '0',
        >= 'a' and <= 'f' => character - 'a' + 10,
        >= 'A' and <= 'F' => character - 'A' + 10,
        _ => -1
    };
}
