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
        LabelTemplateSlot.EmployeeNameplate1 => Employee(slot, "員工名牌 1", includeEnglish: false),
        LabelTemplateSlot.EmployeeNameplate2 => Employee(slot, "員工名牌 2", includeEnglish: true),
        LabelTemplateSlot.MemberNameplate1 => Member(slot, "會員名牌 1", includeValidity: false, includeCard: false),
        LabelTemplateSlot.MemberNameplate2 => Member(slot, "會員名牌 2", includeValidity: true, includeCard: false),
        LabelTemplateSlot.MemberNameplate3 => Member(slot, "會員名牌 3", includeValidity: true, includeCard: true),
        _ => throw new ArgumentOutOfRangeException(nameof(slot))
    };

    private static TemplateDocument Employee(LabelTemplateSlot slot, string name, bool includeEnglish)
    {
        var fields = new List<TemplateField>
        {
            Field("Name", NameplateFields.EmpName, 4, includeEnglish ? 6 : 8, 67, includeEnglish ? 12 : 14, includeEnglish ? 14 : 16, true)
        };
        if (includeEnglish)
            fields.Add(Field("English", NameplateFields.EmpEnglishName, 4, 22, 67, 6, 9, false));
        fields.Add(Field("StaffNo", NameplateFields.StaffNo, 4, includeEnglish ? 30 : 24, 67, 8, 11, false));
        return Document(slot, name, 75, 40, fields);
    }

    private static TemplateDocument Member(LabelTemplateSlot slot, string name, bool includeValidity, bool includeCard)
    {
        var fields = new List<TemplateField>
        {
            Field("Name", NameplateFields.LeagName, 6, 14, 74, 14, 18, true),
            Field("Number", NameplateFields.LeagNo, 6, 30, 74, 8, 12, false)
        };
        if (includeCard)
            fields.Add(Field("Card", NameplateFields.CardNo, 6, 38, 74, 6, 9, false));
        if (includeValidity)
            fields.Add(Field("Valid", NameplateFields.ValidDate, 6, includeCard ? 46 : 40, 74, 6, 9, false));
        return Document(slot, name, 86, 54, fields);
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
