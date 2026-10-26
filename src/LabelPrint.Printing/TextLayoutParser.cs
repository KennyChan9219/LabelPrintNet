using System.Globalization;

namespace LabelPrint.Printing;

public static class TextLayoutParser
{
    public const string FormatId = "LabelPrint.TextLayout.v1";

    public static TemplateDocument Parse(string text, string? sourcePath = null)
    {
        var lines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n');
        var header = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var fields = new List<TemplateField>();
        Dictionary<string, string>? section = null;
        string? sectionName = null;

        foreach (var raw in lines)
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith(';') || line.StartsWith('#'))
                continue;

            if (line.StartsWith('[') && line.EndsWith(']'))
            {
                Flush();
                sectionName = line[1..^1].Trim();
                section = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                continue;
            }

            var separator = line.IndexOf('=');
            if (separator <= 0)
                throw new FormatException($"應為 key=value 或 [section]，卻見到「{line}」。");

            var key = line[..separator].Trim();
            var value = line[(separator + 1)..].Trim();
            if (section is null)
                header[key] = value;
            else
                section[key] = value;
        }

        Flush();

        if (!header.TryGetValue("Format", out var format) || !string.Equals(format, FormatId, StringComparison.Ordinal))
            throw new FormatException($"範本格式必須是 {FormatId}。");

        var width = ParseDouble(Require(header, "WidthMm"), "WidthMm");
        var height = ParseDouble(Require(header, "HeightMm"), "HeightMm");
        if (width is <= 0 or > 500 || height is <= 0 or > 500)
            throw new FormatException("WidthMm 與 HeightMm 必須大於 0，且不可超過 500。");

        var copies = 1;
        if (header.TryGetValue("Copies", out var copiesText) && copiesText.Length > 0)
        {
            if (!int.TryParse(copiesText, NumberStyles.Integer, CultureInfo.InvariantCulture, out copies) || copies is < 1 or > 20)
                throw new FormatException("Copies 必須是 1 至 20 的整數。");
        }

        header.TryGetValue("Background", out var background);
        return new TemplateDocument
        {
            Format = FormatId,
            Name = Require(header, "Name"),
            WidthMm = width,
            HeightMm = height,
            BackgroundPath = string.IsNullOrWhiteSpace(background) ? null : background,
            Copies = copies,
            SourcePath = sourcePath,
            Fields = fields
        };

        void Flush()
        {
            if (section is null || sectionName is null)
                return;
            if (!sectionName.StartsWith("Field.", StringComparison.OrdinalIgnoreCase))
                throw new FormatException($"不明區段 [{sectionName}]。請使用 [Field.Name]。");

            var fieldName = sectionName["Field.".Length..].Trim();
            if (fieldName.Length == 0)
                throw new FormatException("欄位區段需要名稱。");

            fields.Add(ParseField(fieldName, section));
            section = null;
            sectionName = null;
        }
    }

    private static TemplateField ParseField(string name, Dictionary<string, string> section)
    {
        var source = Require(section, "Source");
        if (!NameplateFields.All.Contains(source, StringComparer.OrdinalIgnoreCase))
        {
            throw new FormatException(
                $"欄位 {name} 使用不明來源「{source}」。已知來源：{string.Join(", ", NameplateFields.All)}。");
        }

        var align = TextAlign.Left;
        if (section.TryGetValue("Align", out var alignText) && alignText.Length > 0)
        {
            if (!Enum.TryParse(alignText, ignoreCase: true, out align))
                throw new FormatException($"欄位 {name} 的 Align「{alignText}」不明。");
        }

        var bold = false;
        if (section.TryGetValue("Bold", out var boldText) && boldText.Length > 0)
        {
            if (!bool.TryParse(boldText, out bold))
                throw new FormatException($"欄位 {name} 的 Bold 必須是 true 或 false。");
        }

        var size = 12f;
        if (section.TryGetValue("SizePt", out var sizeText) && sizeText.Length > 0)
        {
            if (!float.TryParse(sizeText, NumberStyles.Float, CultureInfo.InvariantCulture, out size) || size is < 4 or > 96)
                throw new FormatException($"欄位 {name} 的 SizePt 必須介乎 4 至 96。");
        }

        section.TryGetValue("Font", out var font);
        section.TryGetValue("Color", out var color);
        return new TemplateField
        {
            Name = name,
            Source = source,
            Xmm = ParseDouble(Require(section, "Xmm"), $"{name}.Xmm"),
            Ymm = ParseDouble(Require(section, "Ymm"), $"{name}.Ymm"),
            WidthMm = ParseDouble(Require(section, "WidthMm"), $"{name}.WidthMm"),
            HeightMm = ParseDouble(Require(section, "HeightMm"), $"{name}.HeightMm"),
            FontFamily = string.IsNullOrWhiteSpace(font) ? "Arial" : font,
            SizePt = size,
            Bold = bold,
            Align = align,
            ColorHex = string.IsNullOrWhiteSpace(color) ? "#000000" : color
        };
    }

    private static string Require(Dictionary<string, string> values, string key)
    {
        if (!values.TryGetValue(key, out var value) || string.IsNullOrWhiteSpace(value))
            throw new FormatException($"缺少 {key}。");
        return value.Trim();
    }

    private static double ParseDouble(string text, string name)
    {
        if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            throw new FormatException($"{name} 必須是數字。");
        return value;
    }
}
