namespace LabelPrint;

public enum LabelTemplateSlot
{
    EmployeeNameplate1,
    EmployeeNameplate2,
    MemberNameplate1,
    MemberNameplate2,
    MemberNameplate3
}

public enum TemplateLoadStatus
{
    Loaded,
    Missing,
    UnrecognizedBinary,
    InvalidText
}

public enum TextAlign
{
    Left,
    Center,
    Right
}

public static class LabelTemplateFiles
{
    public static string FileName(this LabelTemplateSlot slot) => slot switch
    {
        LabelTemplateSlot.EmployeeNameplate1 => "Empnameplate1.lbl",
        LabelTemplateSlot.EmployeeNameplate2 => "Empnameplate2.lbl",
        LabelTemplateSlot.MemberNameplate1 => "nameplate1.lbl",
        LabelTemplateSlot.MemberNameplate2 => "nameplate2.lbl",
        LabelTemplateSlot.MemberNameplate3 => "nameplate3.lbl",
        _ => throw new ArgumentOutOfRangeException(nameof(slot))
    };

    public static bool IsEmployee(this LabelTemplateSlot slot) =>
        slot is LabelTemplateSlot.EmployeeNameplate1 or LabelTemplateSlot.EmployeeNameplate2;

    public static IReadOnlyList<LabelTemplateSlot> All { get; } = Enum.GetValues<LabelTemplateSlot>();
}

public sealed class TemplateField
{
    public required string Name { get; init; }

    public required string Source { get; init; }

    public double Xmm { get; init; }

    public double Ymm { get; init; }

    public double WidthMm { get; init; }

    public double HeightMm { get; init; }

    public string FontFamily { get; init; } = "Arial";

    public float SizePt { get; init; } = 12;

    public bool Bold { get; init; }

    public TextAlign Align { get; init; } = TextAlign.Left;

    public string ColorHex { get; init; } = "#000000";
}

public sealed class TemplateDocument
{
    public required string Format { get; init; }

    public required string Name { get; init; }

    public double WidthMm { get; init; }

    public double HeightMm { get; init; }

    public string? BackgroundPath { get; init; }

    public int Copies { get; init; } = 1;

    public string? SourcePath { get; init; }

    public IReadOnlyList<TemplateField> Fields { get; init; } = [];
}

public sealed class TemplateLoadResult
{
    public required LabelTemplateSlot Slot { get; init; }

    public required string FileName { get; init; }

    public string? ResolvedPath { get; init; }

    public TemplateLoadStatus Status { get; init; }

    public TemplateDocument? Document { get; init; }

    public string Message { get; init; } = "";
}

public sealed class NameplateJob
{
    public required TemplateLoadResult Template { get; init; }

    public required NameplateBinding Binding { get; init; }

    public required TemplateDocument Layout { get; init; }

    public required string DocumentName { get; init; }

    public int Copies { get; init; } = 1;

    public bool UsedFallbackLayout =>
        Template.Status != TemplateLoadStatus.Loaded || Template.Document is null;
}
