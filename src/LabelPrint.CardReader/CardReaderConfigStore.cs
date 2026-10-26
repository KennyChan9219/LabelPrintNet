using System.Globalization;
using System.Text;

namespace LabelPrint.CardReader;

public static class CardReaderConfigStore
{
    public const string FileName = "CardReader.config";

    public static CardReaderSettings Load(string path)
    {
        try
        {
            return Parse(File.ReadAllText(path));
        }
        catch (Exception ex) when (ex is FormatException or InvalidOperationException)
        {
            throw new InvalidOperationException($"Could not read {path}. {ex.Message}", ex);
        }
    }

    public static CardReaderSettings Parse(string text)
    {
        var settings = new CardReaderSettings();
        string? section = null;
        var sawReader = false;
        foreach (var raw in text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith(';') || line.StartsWith('#'))
                continue;

            if (line.StartsWith('[') && line.EndsWith(']'))
            {
                section = line[1..^1].Trim();
                if (!section.Equals("Reader", StringComparison.OrdinalIgnoreCase))
                    throw new FormatException($"Unknown section [{section}]. Expected [Reader].");
                sawReader = true;
                continue;
            }

            if (!string.Equals(section, "Reader", StringComparison.OrdinalIgnoreCase))
                throw new FormatException("Keys must be under [Reader].");

            var separator = line.IndexOf('=');
            if (separator <= 0)
                throw new FormatException($"Expected key=value, saw '{line}'.");

            var key = line[..separator].Trim();
            var value = line[(separator + 1)..].Trim();
            Apply(settings, key, value);
            sawReader = true;
        }

        if (!sawReader)
            return settings;

        Validate(settings);
        return settings;
    }

    public static void Save(string path, CardReaderSettings settings)
    {
        Validate(settings);
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        var initType = settings.InitType is int type
            ? $"InitType={type.ToString(CultureInfo.InvariantCulture)}{Environment.NewLine}"
            : "; InitType=0" + Environment.NewLine;

        var body = new StringBuilder()
            .AppendLine("; LabelPrint CardReader.config")
            .AppendLine("; Reader model and port only. Do not put a password in this file.")
            .AppendLine("[Reader]")
            .Append("Model=").AppendLine(settings.Model.Trim())
            .Append("Port=").AppendLine(settings.Port.ToString(CultureInfo.InvariantCulture))
            .Append("CardType=").AppendLine(ToConfig(settings.CardType))
            .Append(initType);

        File.WriteAllText(path, body.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private static void Apply(CardReaderSettings settings, string key, string value)
    {
        if (key.Equals("Model", StringComparison.OrdinalIgnoreCase))
        {
            settings.Model = value;
            return;
        }

        if (key.Equals("Port", StringComparison.OrdinalIgnoreCase))
        {
            if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var port))
                throw new FormatException($"Port '{value}' is not a whole number.");
            settings.Port = port;
            return;
        }

        if (key.Equals("CardType", StringComparison.OrdinalIgnoreCase))
        {
            settings.CardType = ParseCardType(value);
            return;
        }

        if (key.Equals("InitType", StringComparison.OrdinalIgnoreCase))
        {
            if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var type))
                throw new FormatException($"InitType '{value}' is not a whole number.");
            settings.InitType = type;
            return;
        }

        if (key.Contains("Password", StringComparison.OrdinalIgnoreCase) || key.Contains("Pin", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "CardReader.config must not contain a password. Set LabelPrint:Card:PasswordHex in user secrets.");
        }

        throw new FormatException($"Unknown key '{key}'.");
    }

    public static void Validate(CardReaderSettings settings)
    {
        var model = settings.Model?.Trim() ?? "";
        if (model.Length is 0 or > 120 || model.Contains('\r') || model.Contains('\n'))
            throw new InvalidOperationException("Reader model must be a single line of 1 to 120 characters.");
        if (settings.Port is < 0 or > short.MaxValue)
            throw new InvalidOperationException("Reader port must be between 0 and 32767.");
        if (settings.InitType is < 0 or > 255)
            throw new InvalidOperationException("InitType must be between 0 and 255 when it is set.");

        settings.Model = model;
    }

    public static CardType ParseCardType(string value)
    {
        var text = value.Replace("_", "", StringComparison.Ordinal).Replace("-", "", StringComparison.Ordinal);
        if (text.Equals("SLE4442", StringComparison.OrdinalIgnoreCase) || text.Equals("4442", StringComparison.OrdinalIgnoreCase))
            return CardType.Sle4442;
        if (text.Equals("SLE4428", StringComparison.OrdinalIgnoreCase) || text.Equals("4428", StringComparison.OrdinalIgnoreCase))
            return CardType.Sle4428;
        throw new FormatException($"Unknown card type '{value}'. Use SLE4442 or SLE4428.");
    }

    public static string ToConfig(CardType type) => type switch
    {
        CardType.Sle4442 => "SLE4442",
        CardType.Sle4428 => "SLE4428",
        _ => throw new ArgumentOutOfRangeException(nameof(type))
    };
}

public static class CardReaderModels
{
    public static IReadOnlyList<string> Known { get; } =
    [
        "ACS CCID USB Reader",
        "ACS ACR38U-CCID",
        "ACS ACR39U-CCID",
        "ACS ACR38U",
        "ACS ACR39U",
        "ACS ACR33U",
        "ACS ACR38 USB Reader"
    ];
}
