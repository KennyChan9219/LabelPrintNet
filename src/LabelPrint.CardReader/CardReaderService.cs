using System.Runtime.Versioning;
using Microsoft.Extensions.Options;

namespace LabelPrint.CardReader;

public sealed class CardReaderService : ICardReader
{
    private readonly IOptions<LabelPrintOptions> _options;
    private readonly string _baseDirectory;
    private readonly string _overrideDirectory;
    private CardReaderSettings _settings;
    private string _settingsPath;

    public CardReaderService(IOptions<LabelPrintOptions> options)
        : this(
            options,
            AppContext.BaseDirectory,
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LabelPrint"))
    {
    }

    public CardReaderService(IOptions<LabelPrintOptions> options, string baseDirectory, string overrideDirectory)
    {
        _options = options;
        _baseDirectory = Path.GetFullPath(baseDirectory);
        _overrideDirectory = Path.GetFullPath(overrideDirectory);
        _settings = new CardReaderSettings();
        _settingsPath = Path.Combine(_baseDirectory, CardReaderConfigStore.FileName);
        Reload();
    }

    public CardReaderSettings Settings => _settings;

    public string SettingsPath => _settingsPath;

    public string NativeLibraryPath => Path.Combine(_baseDirectory, CardReaderFiles.LibraryFileName);

    public bool NativeLibraryPresent => File.Exists(NativeLibraryPath);

    public string DependencyLibraryPath => Path.Combine(_baseDirectory, CardReaderFiles.DependencyFileName);

    public bool DependencyLibraryPresent => File.Exists(DependencyLibraryPath);

    public IReadOnlyList<string> KnownModels => CardReaderModels.Known;

    public void Reload()
    {
        var userFile = Path.Combine(_overrideDirectory, CardReaderConfigStore.FileName);
        var besideExe = Path.Combine(_baseDirectory, CardReaderConfigStore.FileName);
        if (File.Exists(userFile))
        {
            _settingsPath = userFile;
            _settings = CardReaderConfigStore.Load(userFile);
            return;
        }

        if (File.Exists(besideExe))
        {
            _settingsPath = besideExe;
            _settings = CardReaderConfigStore.Load(besideExe);
            return;
        }

        _settingsPath = besideExe;
        _settings = new CardReaderSettings();
    }

    public IReadOnlyList<string> ListConnectedReaders()
    {
        if (!OperatingSystem.IsWindows())
            return [];

        return ListConnectedReadersWindows();
    }

    public void SaveSettings(CardReaderSettings settings)
    {
        settings = CardReaderConfigStore.ForModel(settings.Model);
        var besideExe = Path.Combine(_baseDirectory, CardReaderConfigStore.FileName);
        try
        {
            CardReaderConfigStore.Save(besideExe, settings);
            _settingsPath = besideExe;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            var userFile = Path.Combine(_overrideDirectory, CardReaderConfigStore.FileName);
            CardReaderConfigStore.Save(userFile, settings);
            _settingsPath = userFile;
        }

        _settings = settings;
    }

    public CardProbeResult Probe()
    {
        if (!OperatingSystem.IsWindows())
        {
            return new CardProbeResult
            {
                Success = false,
                Message = "智能卡只可在 Windows 上使用，請將 IC_D600.dll 放在執行檔旁邊。"
            };
        }

        if (!NativeLibraryPresent)
        {
            return new CardProbeResult
            {
                Success = false,
                Message = $"在程式旁邊找不到 IC_D600.dll（{NativeLibraryPath}）。"
            };
        }

        try
        {
            var result = ProbeWindows();
            if (DependencyLibraryPresent)
                return result;

            return new CardProbeResult
            {
                Success = result.Success,
                Code = result.Code,
                Message = result.Message + " 程式旁邊沒有 acsr2032.dll。若讀卡機 DLL 依賴它，請複製到執行檔旁邊。"
            };
        }
        catch (CardReaderException ex)
        {
            return new CardProbeResult
            {
                Success = false,
                Code = ex.NativeCode ?? -1,
                Message = ex.Message
            };
        }
    }

    public CardMemory ReadMemberArea()
    {
        var access = _options.Value.Card;
        return ReadRange(access.MemberNumberOffset, access.MemberNumberLength);
    }

    public CardMemory ReadRange(int offset, int length)
    {
        EnsureEncoding();
        CardCodec.ValidateWindow(Settings.CardType, offset, length);
        _ = CardCodec.ParsePassword(Settings.CardType, _options.Value.Card.PasswordHex);
        if (!OperatingSystem.IsWindows())
            throw WindowsOnly();
        if (!NativeLibraryPresent)
            throw MissingLibrary();

        var bytes = ReadWindows(offset, length);
        return new CardMemory
        {
            Offset = offset,
            Bytes = bytes,
            MemberNumber = SliceMemberNumber(bytes, offset)
        };
    }

    public CardIdRead ReadCardId()
    {
        if (!OperatingSystem.IsWindows())
            throw WindowsOnly();

        return ReadCardIdWindows();
    }

    public void WriteMemberNumber(string memberNumber)
    {
        var access = _options.Value.Card;
        EnsureEncoding();
        CardCodec.ValidateWindow(Settings.CardType, access.MemberNumberOffset, access.MemberNumberLength);
        var data = CardCodec.EncodeMemberNumber(memberNumber, access.MemberNumberLength);
        _ = CardCodec.ParsePassword(Settings.CardType, access.PasswordHex);
        if (!OperatingSystem.IsWindows())
            throw WindowsOnly();
        if (!NativeLibraryPresent)
            throw MissingLibrary();

        WriteWindows(access.MemberNumberOffset, data);
    }

    [SupportedOSPlatform("windows")]
    private IReadOnlyList<string> ListConnectedReadersWindows() => PcscReaders.List();

    [SupportedOSPlatform("windows")]
    private CardIdRead ReadCardIdWindows()
    {
        var failed = DeskMessages.OpenFailed(Settings.Model, ShiyuRunning());
        try
        {
            if (Settings.Model.Equals(CardReaderModelNames.Acr30U, StringComparison.OrdinalIgnoreCase))
            {
                throw new CardReaderException(
                    failed + Environment.NewLine + Environment.NewLine +
                    "TODO(on-machine): ACR30U 經 ACSR2032.DLL（AC_Open / AC_StartSession）讀偏移 248 的 5 個位元組。");
            }

            if (Settings.Model.Equals(CardReaderModelNames.Acr38U, StringComparison.OrdinalIgnoreCase))
                return PcscReaders.ReadAcr38();

            if (!NativeLibraryPresent)
                throw MissingLibrary();

            return IcD600Gateway.ReadRd600P(CreateRequest(failed));
        }
        catch (CardReaderException ex) when (ex.Message == "reader-open-failed")
        {
            throw new CardReaderException(failed, ex.NativeCode);
        }
    }

    private static bool ShiyuRunning()
    {
        try
        {
            return System.Diagnostics.Process.GetProcessesByName("shiyu").Length > 0;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    [SupportedOSPlatform("windows")]
    private CardProbeResult ProbeWindows() => IcD600Gateway.Probe(CreateRequest());

    [SupportedOSPlatform("windows")]
    private byte[] ReadWindows(int offset, int length) => IcD600Gateway.Read(CreateRequest(), offset, length);

    [SupportedOSPlatform("windows")]
    private void WriteWindows(int offset, byte[] data) => IcD600Gateway.Write(CreateRequest(), offset, data);

    private string? SliceMemberNumber(byte[] bytes, int offset)
    {
        var access = _options.Value.Card;
        var start = access.MemberNumberOffset;
        var length = access.MemberNumberLength;
        if (offset > start || offset + bytes.Length < start + length)
            return null;

        return CardCodec.DecodeMemberNumber(bytes.AsSpan(start - offset, length));
    }

    private void EnsureEncoding()
    {
        var encoding = _options.Value.Card.MemberNumberEncoding;
        if (!encoding.Equals("ASCII", StringComparison.OrdinalIgnoreCase))
        {
            throw new CardReaderException(
                "目前只支援 ASCII 會員編號。TODO(on-machine): 請先從傾印確認卡上編碼，再加入其他編碼。");
        }
    }

    private CardIoRequest CreateRequest(string? openFailedMessage = null) => new()
    {
        Settings = _settings,
        Access = _options.Value.Card,
        BaseDirectory = _baseDirectory,
        OpenFailedMessage = openFailedMessage ?? DeskMessages.OpenFailed(_settings.Model, false)
    };

    private CardReaderException MissingLibrary() =>
        new($"在程式旁邊找不到 IC_D600.dll（{NativeLibraryPath}）。");

    private static CardReaderException WindowsOnly() =>
        new("智能卡只可在 Windows 上使用，請將 IC_D600.dll 放在執行檔旁邊。");
}
