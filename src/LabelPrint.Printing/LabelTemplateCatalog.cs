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
            Message = $"{fileName} was not found. Searched: {SearchPathDescription}"
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
                Message = $"TODO(on-machine): {fileName} is larger than 8 MB and was not decoded."
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
                Message = $"{fileName} is a {imageKind} image. Text positions are the built-in overlay. TODO(on-machine): calibrate them against the printed stock."
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
                Message = $"TODO(on-machine): {fileName} is not a text layout or an image. Capture a sample from the gym PC and decode the legacy binary .lbl. Printing uses the built-in layout."
            };
        }

        try
        {
            var document = TextLayoutParser.Parse(Decode(bytes), path);
            document = ResolveBackground(document, path);
            var message = "Loaded text layout.";
            if (!string.IsNullOrWhiteSpace(document.BackgroundPath) && !File.Exists(document.BackgroundPath))
                message = $"Loaded text layout. Background was not found: {document.BackgroundPath}";

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
                Message = $"{fileName} is a text layout but could not be parsed: {ex.Message}"
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
