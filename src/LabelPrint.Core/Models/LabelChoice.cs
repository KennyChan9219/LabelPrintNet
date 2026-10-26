namespace LabelPrint;

/// <summary>
/// Template pick from frmMemberCard. A name shorter than 15 characters uses nameplate 1
/// (or Empnameplate1). Good Luck always uses nameplate3.
/// </summary>
public static class LabelChoice
{
    public const int ShortNameLength = 15;

    public const string StandardValidUntil = "Valid Until";

    public const string GoodLuckValidUntil = "有效期至";

    public const string GoodLuckValidDate = "2020年1月12日";

    public const string GoodLuckFormDate = "12 Jan 2020";

    public const string TrialDateTemplate = "20__-12-31";

    public static LabelTemplateSlot ForMember(bool goodLuck, string name) =>
        goodLuck
            ? LabelTemplateSlot.MemberNameplate3
            : name.Trim().Length < ShortNameLength
                ? LabelTemplateSlot.MemberNameplate1
                : LabelTemplateSlot.MemberNameplate2;

    public static LabelTemplateSlot ForEmployee(string name) =>
        name.Trim().Length < ShortNameLength
            ? LabelTemplateSlot.EmployeeNameplate1
            : LabelTemplateSlot.EmployeeNameplate2;

    public static string PrintedYear(string validDateText)
    {
        var text = validDateText.Trim();
        return text.Length <= 4 ? text : text[..4];
    }

    public static string PrintedValidDate(bool goodLuck, string validDateText) =>
        goodLuck ? GoodLuckValidDate : PrintedYear(validDateText);

    public static bool TrialDateIsReady(string text)
    {
        var value = text ?? "";
        if (value.StartsWith("20__", StringComparison.Ordinal) || value.Length != 10)
            return false;
        return int.TryParse(value.AsSpan(0, 4), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out _);
    }
}

/// <summary>Message text copied from frmMemberCard.</summary>
public static class DeskMessages
{
    public const string AppTitle = "會員卡Label列印系統 V1.6";

    public const string ReaderTitle = "會員卡Label列印系統 V1.0";

    public static string OpenFailed(string model, bool shiyuRunning)
    {
        var closing = shiyuRunning
            ? "- 請先關閉本機世宇系統!" + Environment.NewLine
            : "";
        return $"讀卡器型號: {model}{Environment.NewLine}ＩＣ咭座啟動失敗! {Environment.NewLine}{Environment.NewLine}{closing}- 請檢查讀卡器連線!";
    }
}
