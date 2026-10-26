namespace LabelPrint;

public interface ILeagerRepository
{
    Task<LeagerRecord?> FindByNumberAsync(string leagNo, CancellationToken cancellationToken = default);

    Task<LeagerRecord?> FindByOldCardNumberAsync(string oldCardNo, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LeagerRecord>> SearchByNameAsync(string name, int maxRows = 25, CancellationToken cancellationToken = default);
}

public interface IMtUserRepository
{
    Task<EmployeeRecord?> FindByStaffNumberAsync(string staffNo, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<EmployeeRecord>> SearchByNameAsync(string name, int maxRows = 25, CancellationToken cancellationToken = default);
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
}
