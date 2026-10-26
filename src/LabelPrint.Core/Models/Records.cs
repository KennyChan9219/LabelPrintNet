namespace LabelPrint;

/// <summary>One row from MIS <c>MT_USER</c>.</summary>
public sealed class EmployeeRecord
{
    public required string StaffNumber { get; init; }

    public string EnglishName { get; init; } = "";

    public string ChineseName { get; init; } = "";

    /// <summary>Legacy <c>MU_ID</c>. Loaded for templates; the main form does not display it.</summary>
    public string Id { get; init; } = "";

    public string DisplayName =>
        string.IsNullOrWhiteSpace(ChineseName) ? EnglishName : ChineseName;
}

/// <summary>One row from MIS <c>leager</c> (the legacy spelling).</summary>
public sealed class LeagerRecord
{
    public required string Number { get; init; }

    public string Name { get; init; } = "";

    public string OldCardNumber { get; init; } = "";

    public string ValidDate { get; init; } = "";

    /// <summary>Null when the optional member-code column is not configured.</summary>
    public string? MemberCode { get; init; }

    /// <summary>Null when the optional temp-member column is not configured.</summary>
    public string? TempMember { get; init; }
}
