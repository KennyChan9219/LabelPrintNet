namespace LabelPrint;

public interface ILeagerRepository
{
    Task<LeagerRecord?> FindByNumberAsync(string leagNo, CancellationToken cancellationToken = default);

    Task<LeagerRecord?> FindByOldCardNumberAsync(string oldCardNo, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LeagerRecord>> SearchByNameAsync(string name, int maxRows = 25, CancellationToken cancellationToken = default);

    /// <summary>VB6 <c>dblGetMatchCount("leager", "netflag = 'Y' and CardNo", card)</c>.</summary>
    Task<int> CountActiveByCardAsync(string cardNo, CancellationToken cancellationToken = default);

    /// <summary>Resolves <c>CardNo</c> to <c>leag_no</c>, then loads the label columns.</summary>
    Task<LeagerRecord?> FindLabelByCardAsync(string cardNo, CancellationToken cancellationToken = default);

    /// <summary>VB6 <c>EXECUTE net_c_interface</c> when the card is not yet on this branch.</summary>
    Task RequestCardTransferAsync(string cardNo, CancellationToken cancellationToken = default);
}

public interface IMtUserRepository
{
    Task<EmployeeRecord?> FindByStaffNumberAsync(string staffNo, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<EmployeeRecord>> SearchByNameAsync(string name, int maxRows = 25, CancellationToken cancellationToken = default);

    /// <summary>VB6 employee lookup: enabled <c>MT_USER</c> row whose <c>MU_ID</c> is the card-back number.</summary>
    Task<EmployeeRecord?> FindByCardIdAsync(string cardId, CancellationToken cancellationToken = default);
}

public interface ILabelTemplateCatalog
{
    string SearchPathDescription { get; }

    TemplateLoadResult Load(LabelTemplateSlot slot);

    IReadOnlyList<TemplateLoadResult> LoadAll();
}

public interface INameplatePrintPipeline
{
    NameplateJob Create(LabelTemplateSlot slot, NameplateBinding binding);
}

public interface ICardReader
{
    CardReaderSettings Settings { get; }

    string SettingsPath { get; }

    bool NativeLibraryPresent { get; }

    string NativeLibraryPath { get; }

    bool DependencyLibraryPresent { get; }

    string DependencyLibraryPath { get; }

    IReadOnlyList<string> KnownModels { get; }

    void Reload();

    IReadOnlyList<string> ListConnectedReaders();

    void SaveSettings(CardReaderSettings settings);

    CardProbeResult Probe();

    CardMemory ReadMemberArea();

    CardMemory ReadRange(int offset, int length);

    void WriteMemberNumber(string memberNumber);

    /// <summary>Reads the 5-byte id at offset 248 the way frmMemberCard does. No PIN is sent.</summary>
    CardIdRead ReadCardId();
}
