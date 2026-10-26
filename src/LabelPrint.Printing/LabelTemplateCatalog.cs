using System.Text;

namespace LabelPrint.Printing;

public sealed class LabelTemplateCatalog : ILabelTemplateCatalog
{
    public const int MaxBytes = 8 * 1024 * 1024;

    private readonly IReadOnlyList<string> _directories;

    public LabelTemplateCatalog(string baseDirectory, string? configuredDirectory)
    {
        if (string.IsNullOrWhiteSpace(baseDirectory))
            throw new ArgumentException("Base directory is required.", nameof(baseDirectory));

        var directories = new List<string>();
        if (!string.IsNullOrWhiteSpace(configuredDirectory))
            directories.Add(Path.GetFullPath(configuredDirectory));
        directories.Add(Path.GetFullPath(baseDirectory));
        directories.Add(Path.GetFullPath(Path.Combine(baseDirectory, "templates")));
        _directories = directories.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    public string SearchPathDescription => string.Join(" | ", _directories);

    public TemplateLoadResult Load(LabelTemplateSlot slot)
    {
        var fileName = slot.FileName();
        foreach (var directory in _directories)
        {
            var candidate = Path.Combine(directory, fileName);
            if (File.Exists(candidate))
                return LoadFile(slot, fileName, candidate);
        }

        return new TemplateLoadResult
        {
            Slot = slot,
            FileName = fileName,
            Status = TemplateLoadStatus.Missing,
            Message = $"{fileName} 找不到。已搜尋：{SearchPathDescription}"
        };
    }

    public IReadOnlyList<TemplateLoadResult> LoadAll()
    {
        var results = new List<TemplateLoadResult>(LabelTemplateFiles.All.Count);
        foreach (var slot in LabelTemplateFiles.All)
            results.Add(Load(slot));
        return results;
    }

    private static TemplateLoadResult LoadFile(LabelTemplateSlot slot, string fileName, string path)
    {
        var info = new FileInfo(path);
        if (info.Length > MaxBytes)
        {
            return new TemplateLoadResult
            {
                Slot = slot,
                FileName = fileName,
                ResolvedPath = path,
                Status = TemplateLoadStatus.UnrecognizedBinary,
                Message = $"TODO(on-machine): {fileName} 大於 8 MB，未解碼。"
            };
        }

        var bytes = File.ReadAllBytes(path);
        if (TryImage(bytes, out var imageKind))
        {
            var builtin = BuiltInLayouts.For(slot);
            return new TemplateLoadResult
            {
                Slot = slot,
                FileName = fileName,
                ResolvedPath = path,
                Status = TemplateLoadStatus.Loaded,
                Document = new TemplateDocument
                {
                    Format = "image",
                    Name = builtin.Name,
                    WidthMm = builtin.WidthMm,
                    HeightMm = builtin.HeightMm,
                    BackgroundPath = path,
                    Copies = 1,
                    SourcePath = path,
                    Fields = builtin.Fields
                },
                Message = $"{fileName} 是 {imageKind} 圖片。文字位置使用內建疊加。TODO(on-machine): 請對照實際標籤校準。"
            };
        }

        if (!LooksLikeTextLayout(bytes))
        {
            return new TemplateLoadResult
            {
                Slot = slot,
                FileName = fileName,
                ResolvedPath = path,
                Status = TemplateLoadStatus.UnrecognizedBinary,
                Message = $"TODO(on-machine): {fileName} 不是文字版面或圖片。請從櫃檯電腦取得樣本並解碼舊版二進位 .lbl。列印會使用內建版面。"
            };
        }

        try
        {
            var document = TextLayoutParser.Parse(Decode(bytes), path);
            document = ResolveBackground(document, path);
            var message = "已載入文字版面。";
            if (!string.IsNullOrWhiteSpace(document.BackgroundPath) && !File.Exists(document.BackgroundPath))
                message = $"已載入文字版面。找不到背景：{document.BackgroundPath}";

            return new TemplateLoadResult
            {
                Slot = slot,
                FileName = fileName,
                ResolvedPath = path,
                Status = TemplateLoadStatus.Loaded,
                Document = document,
                Message = message
            };
        }
        catch (FormatException ex)
        {
            return new TemplateLoadResult
            {
                Slot = slot,
                FileName = fileName,
                ResolvedPath = path,
                Status = TemplateLoadStatus.InvalidText,
                Message = $"{fileName} 是文字版面，但無法解析：{ex.Message}"
            };
        }
    }

    private static TemplateDocument ResolveBackground(TemplateDocument document, string lblPath)
    {
        if (string.IsNullOrWhiteSpace(document.BackgroundPath))
            return document;

        var background = document.BackgroundPath;
        if (!Path.IsPathRooted(background))
            background = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(lblPath) ?? "", background));

        return new TemplateDocument
        {
            Format = document.Format,
            Name = document.Name,
            WidthMm = document.WidthMm,
            HeightMm = document.HeightMm,
            BackgroundPath = background,
            Copies = document.Copies,
            SourcePath = document.SourcePath,
            Fields = document.Fields
        };
    }

    internal static bool LooksLikeTextLayout(ReadOnlySpan<byte> bytes)
    {
        var sample = bytes.Length > 4096 ? bytes[..4096] : bytes;
        var text = Decode(sample.ToArray());
        return text.Contains(TextLayoutParser.FormatId, StringComparison.Ordinal);
    }

    private static string Decode(byte[] bytes)
    {
        if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
            return Encoding.Unicode.GetString(bytes);
        if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
            return Encoding.BigEndianUnicode.GetString(bytes);
        return Encoding.UTF8.GetString(bytes);
    }

    private static bool TryImage(ReadOnlySpan<byte> data, out string kind)
    {
        if (data.Length >= 3 && data[0] == 0xFF && data[1] == 0xD8 && data[2] == 0xFF)
        {
            kind = "JPEG";
            return true;
        }

        if (data.Length >= 8 && data[0] == 0x89 && data[1] == 0x50 && data[2] == 0x4E && data[3] == 0x47)
        {
            kind = "PNG";
            return true;
        }

        if (data.Length >= 2 && data[0] == (byte)'B' && data[1] == (byte)'M')
        {
            kind = "BMP";
            return true;
        }

        if (data.Length >= 6 && data[0] == (byte)'G' && data[1] == (byte)'I' && data[2] == (byte)'F')
        {
            kind = "GIF";
            return true;
        }

        kind = "";
        return false;
    }
}
