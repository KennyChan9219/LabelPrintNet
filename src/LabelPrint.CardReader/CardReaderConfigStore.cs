using System.Text;

namespace LabelPrint.CardReader;

/// <summary>
/// CardReader.config in the VB6 program is one token: ACR30U, ACR38U, or RD600P.
/// The file never stores a password.
/// </summary>
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
        if (text.Contains("Password", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("Pin=", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "CardReader.config 不可包含密碼。請在 user secrets 設定 LabelPrint:Card:PasswordHex。");
        }

        var token = FirstToken(text);
        var model = CardReaderModelNames.Normalize(token);
        return ForModel(model);
    }

    public static void Save(string path, CardReaderSettings settings)
    {
        var model = CardReaderModelNames.Normalize(settings.Model);
        if (!CardReaderModelNames.Known.Contains(model, StringComparer.Ordinal))
            throw new InvalidOperationException("讀卡機型號必須是 ACR30U、ACR38U 或 RD600P。");

        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        File.WriteAllText(path, model + Environment.NewLine, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    public static CardReaderSettings ForModel(string model)
    {
        var name = CardReaderModelNames.Normalize(model);
        return new CardReaderSettings
        {
            Model = name,
            Port = 101,
            CardType = CardType.Sle4442,
            InitType = 16
        };
    }

    private static string FirstToken(string text)
    {
        foreach (var raw in text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith(';') || line.StartsWith('#'))
                continue;

            var token = line.Split([' ', '\t', '='], StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            if (!string.IsNullOrEmpty(token))
                return token;
        }

        return "";
    }
}

public static class CardReaderModels
{
    public static IReadOnlyList<string> Known => CardReaderModelNames.Known;
}
