namespace LabelPrint;

internal sealed class DeskSession
{
    public EmployeeDraft Employee { get; } = new();

    public MemberDraft Member { get; } = new();

    public LabelTemplateSlot EmployeeTemplate { get; set; } = LabelTemplateSlot.EmployeeNameplate1;

    public LabelTemplateSlot MemberTemplate { get; set; } = LabelTemplateSlot.MemberNameplate1;
}

internal sealed class EmployeeDraft
{
    public string StaffNumber { get; set; } = "";

    public string EnglishName { get; set; } = "";

    public string ChineseName { get; set; } = "";

    public string Id { get; set; } = "";

    public string PrintedName { get; set; } = "";

    public void Apply(EmployeeRecord record)
    {
        StaffNumber = record.StaffNumber;
        EnglishName = record.EnglishName;
        ChineseName = record.ChineseName;
        Id = record.Id;
        PrintedName = record.DisplayName;
    }

    public NameplateBinding ToBinding() =>
        NameplateBinding.ForEmployee(StaffNumber, PrintedName, EnglishName, ChineseName, Id);
}

internal sealed class MemberDraft
{
    public string Number { get; set; } = "";

    public string Name { get; set; } = "";

    public string CardNumber { get; set; } = "";

    public string MemberCode { get; set; } = "";

    public string ValidDate { get; set; } = "";

    public string TempMember { get; set; } = "";

    public void Apply(LeagerRecord record)
    {
        Number = record.Number;
        Name = record.Name;
        if (!string.IsNullOrWhiteSpace(record.OldCardNumber))
            CardNumber = record.OldCardNumber;
        ValidDate = record.ValidDate;
        if (record.MemberCode is not null)
            MemberCode = record.MemberCode;
        if (record.TempMember is not null)
            TempMember = record.TempMember;
    }

    public NameplateBinding ToBinding() =>
        NameplateBinding.ForMember(Number, Name, CardNumber, MemberCode, ValidDate, TempMember);
}
