namespace LabelPrint.Printing;

/// <summary>
/// Layouts used when a legacy <c>.lbl</c> is missing or still an undecoded binary.
/// Measurements are a starting point for CR80-sized cards and badge stock.
/// TODO(on-machine): measure the real label stock and match the VB6 picture boxes.
/// </summary>
public static class BuiltInLayouts
{
    public static TemplateDocument For(LabelTemplateSlot slot) => slot switch
    {
        LabelTemplateSlot.EmployeeNameplate1 => Plate(slot, "員工名牌 1", includeValid: false),
        LabelTemplateSlot.EmployeeNameplate2 => Plate(slot, "員工名牌 2", includeValid: false),
        LabelTemplateSlot.MemberNameplate1 => Plate(slot, "會員名牌 1", includeValid: true),
        LabelTemplateSlot.MemberNameplate2 => Plate(slot, "會員名牌 2", includeValid: true),
        LabelTemplateSlot.MemberNameplate3 => Plate(slot, "會員名牌 3", includeValid: true),
        _ => throw new ArgumentOutOfRangeException(nameof(slot))
    };

    private static TemplateDocument Plate(LabelTemplateSlot slot, string name, bool includeValid)
    {
        var fields = new List<TemplateField>
        {
            Field("MemID", NameplateFields.MemID, 6, 8, 74, 8, 11, true),
            Field("MemName", NameplateFields.MemName, 6, 18, 74, 12, includeValid ? 16 : 14, true)
        };
        if (includeValid)
            fields.Add(Field("ValidDate", NameplateFields.ValidDate, 6, 34, 74, 8, 11, true));
        var wide = slot.IsEmployee() ? 75d : 86d;
        var high = slot.IsEmployee() ? 40d : 54d;
        return Document(slot, name, wide, high, fields);
    }

    private static TemplateDocument Document(
        LabelTemplateSlot slot,
        string name,
        double width,
        double height,
        List<TemplateField> fields) => new()
    {
        Format = "built-in",
        Name = name,
        WidthMm = width,
        HeightMm = height,
        Copies = 1,
        SourcePath = slot.FileName(),
        Fields = fields
    };

    private static TemplateField Field(
        string name,
        string source,
        double x,
        double y,
        double width,
        double height,
        float size,
        bool bold) => new()
    {
        Name = name,
        Source = source,
        Xmm = x,
        Ymm = y,
        WidthMm = width,
        HeightMm = height,
        SizePt = size,
        Bold = bold,
        Align = TextAlign.Center,
        FontFamily = "Arial"
    };
}
