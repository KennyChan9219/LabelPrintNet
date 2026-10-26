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
    public string Model { get; set; } = "ACS CCID USB Reader";

    /// <summary>
    /// Vendor port number. 100 is the usual USB convention in this family of DLLs and is unconfirmed
    /// for the Jumping Gym copy of IC_D600.dll.
    /// </summary>
    public int Port { get; set; } = 100;

    public CardType CardType { get; set; } = CardType.Sle4442;

    /// <summary>When set, probe/read/write call <c>IC_InitType</c> with this value.</summary>
    public int? InitType { get; set; }
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
    public static int Capacity(CardType type) => type switch
    {
        CardType.Sle4442 => 256,
        CardType.Sle4428 => 1024,
        _ => throw new ArgumentOutOfRangeException(nameof(type))
    };

    public static void ValidateWindow(CardType type, int offset, int length)
    {
        if (offset < 0)
            throw new ArgumentOutOfRangeException(nameof(offset), "Card offset cannot be negative.");
        if (length <= 0)
            throw new ArgumentOutOfRangeException(nameof(length), "Card length must be positive.");

        var capacity = Capacity(type);
        if (offset > capacity || length > capacity - offset)
        {
            throw new ArgumentOutOfRangeException(
                nameof(length),
                $"Card window {offset}+{length} is outside {type} main memory ({capacity} bytes).");
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
                $"{type} password must be {expected} bytes ({expected * 2} hex digits).");
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
                throw new FormatException("Password hex contains a non-hex character.");

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
            throw new FormatException("Password hex has an odd number of digits.");

        return buffer.ToArray();
    }

    public static byte[] EncodeMemberNumber(string memberNumber, int length)
    {
        if (length is <= 0 or > 64)
            throw new ArgumentOutOfRangeException(nameof(length));

        var text = memberNumber.Trim();
        if (text.Length == 0)
            throw new ArgumentException("Member number is empty.", nameof(memberNumber));
        if (text.Length > length)
            throw new ArgumentException($"Member number is longer than the card field ({length}).", nameof(memberNumber));

        foreach (var character in text)
        {
            if (character is < (char)32 or > (char)126)
                throw new ArgumentException("Member number on the card must be printable ASCII.", nameof(memberNumber));
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
