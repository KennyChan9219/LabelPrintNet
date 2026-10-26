namespace LabelPrint;

public sealed class MisConnectionOptions
{
    public const string SectionName = "ConnectionStrings";

    /// <summary>MIS SQL Server connection string. Empty unless user secrets, a local file, or the environment set it.</summary>
    public string Mis { get; set; } = "";
}

public sealed class LabelPrintOptions
{
    public const string SectionName = "LabelPrint";

    /// <summary>Optional extra directory searched before the application directory.</summary>
    public string TemplateDirectory { get; set; } = "";

    public int CommandTimeoutSeconds { get; set; } = 30;

    /// <summary>Empty uses the Windows default printer.</summary>
    public string PrinterName { get; set; } = "";

    public LeagerTableOptions Leager { get; set; } = new();

    public MtUserTableOptions MtUser { get; set; } = new();

    public CardOptions Card { get; set; } = new();
}

public sealed class LeagerTableOptions
{
    public string Table { get; set; } = "leager";

    public string NumberColumn { get; set; } = "leag_no";

    public string NameColumn { get; set; } = "leag_name";

    public string OldCardNumberColumn { get; set; } = "oldcardno";

    public string ValidDateColumn { get; set; } = "validdate";

    /// <summary>Blank until the MIS column is confirmed. The desk can still type a member code.</summary>
    public string MemberCodeColumn { get; set; } = "";

    /// <summary>Blank until the MIS column is confirmed.</summary>
    public string TempMemberColumn { get; set; } = "";
}

public sealed class MtUserTableOptions
{
    public string Table { get; set; } = "MT_USER";

    public string StaffNumberColumn { get; set; } = "MU_StaffNo";

    public string EnglishNameColumn { get; set; } = "MU_Name";

    public string ChineseNameColumn { get; set; } = "MU_CHIName";

    public string IdColumn { get; set; } = "MU_ID";
}

public sealed class CardOptions
{
    /// <summary>SLE4442 is 3 bytes, SLE4428 is 2. Empty skips the PIN call. Never commit a real PIN.</summary>
    public string PasswordHex { get; set; } = "";

    public int MemberNumberOffset { get; set; } = 32;

    public int MemberNumberLength { get; set; } = 16;

    public string MemberNumberEncoding { get; set; } = "ASCII";

    /// <summary>
    /// When true, a negative <c>IC_InitComm</c> result is a failure and any other result is the handle.
    /// When false, 0 is success and the handle is 0.
    /// </summary>
    public bool InitReturnsHandle { get; set; } = true;

    /// <summary>Return value from the check-card export that means a card is present.</summary>
    public int CheckCardPresentCode { get; set; } = 0;

    /// <summary>Return value from read/write exports that means success.</summary>
    public int IoSuccessCode { get; set; } = 0;

    public int BeepMilliseconds { get; set; } = 30;

    public NativeExportOptions Exports { get; set; } = new();
}

/// <summary>
/// DLL export names. These match the usual VB6 declares for a D600-style SLE4428/4442 library.
/// TODO(on-machine): confirm every name with <c>dumpbin /exports IC_D600.dll</c> before writing a card.
/// </summary>
public sealed class NativeExportOptions
{
    public string InitComm { get; set; } = "IC_InitComm";

    public string ExitComm { get; set; } = "IC_ExitComm";

    public string InitType { get; set; } = "IC_InitType";

    public string Status { get; set; } = "IC_Status";

    public string CheckCard { get; set; } = "IC_CheckCard";

    public string Beep { get; set; } = "IC_DevBeep";

    public string Choose4442 { get; set; } = "IC_Choose_4442";

    public string Choose4428 { get; set; } = "IC_Choose_4428";

    public string CheckPass4442 { get; set; } = "IC_CheckPass_SLE4442";

    public string CheckPass4428 { get; set; } = "IC_CheckPass_SLE4428";

    public string Read4442 { get; set; } = "IC_Read_SLE4442";

    public string Write4442 { get; set; } = "IC_Write_SLE4442";

    public string Read4428 { get; set; } = "IC_Read_SLE4428";

    public string Write4428 { get; set; } = "IC_Write_SLE4428";

    public string ReadCount4442 { get; set; } = "IC_ReadCount_SLE4442";

    public string ReadCount4428 { get; set; } = "IC_ReadCount_SLE4428";
}
