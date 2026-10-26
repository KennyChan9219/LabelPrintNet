namespace LabelPrint;

/// <summary>Field names a <c>.lbl</c> template may reference in <c>Source=</c>.</summary>
public static class NameplateFields
{
    public const string StaffNo = "StaffNo";
    public const string EmpName = "EmpName";
    public const string EmpEnglishName = "EmpEnglishName";
    public const string EmpChineseName = "EmpChineseName";
    public const string EmpId = "EmpId";
    public const string LeagNo = "LeagNo";
    public const string LeagName = "LeagName";
    public const string OldCardNo = "OldCardNo";
    public const string CardNo = "CardNo";
    public const string MemberCode = "MemberCode";
    public const string ValidDate = "ValidDate";
    public const string TempMember = "TempMember";

    /// <summary>BrssCom.Document field filled by <c>DoPrint</c> / <c>DoPrintEmp</c>.</summary>
    public const string MemID = "MemID";

    /// <summary>BrssCom.Document field filled by <c>DoPrint</c> / <c>DoPrintEmp</c>.</summary>
    public const string MemName = "MemName";

    public static IReadOnlyList<string> All { get; } =
    [
        StaffNo,
        EmpName,
        EmpEnglishName,
        EmpChineseName,
        EmpId,
        LeagNo,
        LeagName,
        OldCardNo,
        CardNo,
        MemberCode,
        ValidDate,
        TempMember,
        MemID,
        MemName
    ];
}

/// <summary>Values substituted into a nameplate at print time. Missing sources print as blank.</summary>
public sealed class NameplateBinding
{
    private readonly Dictionary<string, string> _fields;

    private NameplateBinding(Dictionary<string, string> fields)
    {
        _fields = fields;
    }

    public string Get(string source) =>
        _fields.TryGetValue(source, out var value) ? value : "";

    public static NameplateBinding ForEmployee(
        string staffNumber,
        string printedName,
        string englishName,
        string chineseName,
        string id)
    {
        return new NameplateBinding(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [NameplateFields.StaffNo] = staffNumber.Trim(),
            [NameplateFields.EmpName] = printedName.Trim(),
            [NameplateFields.EmpEnglishName] = englishName.Trim(),
            [NameplateFields.EmpChineseName] = chineseName.Trim(),
            [NameplateFields.EmpId] = id.Trim(),
            [NameplateFields.MemID] = staffNumber.Trim(),
            [NameplateFields.MemName] = printedName.Trim()
        });
    }

    public static NameplateBinding ForMember(
        string number,
        string name,
        string cardNumber,
        string memberCode,
        string validDate,
        string tempMember)
    {
        var card = cardNumber.Trim();
        return new NameplateBinding(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [NameplateFields.LeagNo] = number.Trim(),
            [NameplateFields.LeagName] = name.Trim(),
            [NameplateFields.OldCardNo] = card,
            [NameplateFields.CardNo] = card,
            [NameplateFields.MemberCode] = memberCode.Trim(),
            [NameplateFields.ValidDate] = validDate.Trim(),
            [NameplateFields.TempMember] = tempMember.Trim(),
            [NameplateFields.MemID] = (string.IsNullOrWhiteSpace(memberCode) ? number : memberCode).Trim(),
            [NameplateFields.MemName] = name.Trim()
        });
    }

    /// <summary>The three BrssCom fields the VB6 print path actually sets.</summary>
    public static NameplateBinding ForLabel(string memId, string memName, string validDate)
    {
        var id = memId.Trim();
        var name = memName.Trim();
        var valid = validDate.Trim();
        return new NameplateBinding(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [NameplateFields.MemID] = id,
            [NameplateFields.MemName] = name,
            [NameplateFields.ValidDate] = valid,
            [NameplateFields.StaffNo] = id,
            [NameplateFields.EmpName] = name,
            [NameplateFields.LeagNo] = id,
            [NameplateFields.LeagName] = name,
            [NameplateFields.MemberCode] = id
        });
    }
}
