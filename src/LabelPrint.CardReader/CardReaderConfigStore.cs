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
            throw new InvalidOperationException($"無法讀取 {path}。{ex.Message}", ex);
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
                    throw new FormatException($"不明區段 [{section}]。應為 [Reader]。");
                sawReader = true;
                continue;
            }

            if (!string.Equals(section, "Reader", StringComparison.OrdinalIgnoreCase))
                throw new FormatException("鍵必須放在 [Reader] 之下。");

            var separator = line.IndexOf('=');
            if (separator <= 0)
                throw new FormatException($"應為 key=value，卻見到「{line}」。");

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
            .AppendLine("; 只記錄讀卡機型號與連接埠。不要在此檔放入密碼。")
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
                throw new FormatException($"連接埠「{value}」不是整數。");
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
                throw new FormatException($"InitType「{value}」不是整數。");
            settings.InitType = type;
            return;
        }

        if (key.Contains("Password", StringComparison.OrdinalIgnoreCase) || key.Contains("Pin", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "CardReader.config 不可包含密碼。請在 user secrets 設定 LabelPrint:Card:PasswordHex。");
        }

        throw new FormatException($"不明鍵「{key}」。");
    }

    public static void Validate(CardReaderSettings settings)
    {
        var model = settings.Model?.Trim() ?? "";
        if (model.Length is 0 or > 120 || model.Contains('\r') || model.Contains('\n'))
            throw new InvalidOperationException("讀卡機型號必須是 1 至 120 個字元的單行。");
        if (settings.Port is < 0 or > short.MaxValue)
            throw new InvalidOperationException("連接埠必須介乎 0 至 32767。");
        if (settings.InitType is < 0 or > 255)
            throw new InvalidOperationException("若設定 InitType，必須介乎 0 至 255。");

        settings.Model = model;
    }

    public static CardType ParseCardType(string value)
    {
        var text = value.Replace("_", "", StringComparison.Ordinal).Replace("-", "", StringComparison.Ordinal);
        if (text.Equals("SLE4442", StringComparison.OrdinalIgnoreCase) || text.Equals("4442", StringComparison.OrdinalIgnoreCase))
            return CardType.Sle4442;
        if (text.Equals("SLE4428", StringComparison.OrdinalIgnoreCase) || text.Equals("4428", StringComparison.OrdinalIgnoreCase))
            return CardType.Sle4428;
        throw new FormatException($"不明卡類「{value}」。請使用 SLE4442 或 SLE4428。");
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
