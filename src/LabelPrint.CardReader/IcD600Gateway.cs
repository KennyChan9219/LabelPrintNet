using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace LabelPrint.CardReader;

internal sealed class CardIoRequest
{
    public required CardReaderSettings Settings { get; init; }

    public required CardOptions Access { get; init; }

    public required string BaseDirectory { get; init; }
}

internal static class CardReaderFiles
{
    public const string LibraryFileName = "IC_D600.dll";
    public const string DependencyFileName = "acsr2032.dll";
}

/// <summary>
/// Dynamic loader for IC_D600.dll. Export names and signatures are assumptions, listed in
/// <see cref="NativeExportOptions"/>, until they are confirmed on a gym PC.
/// TODO(on-machine): a mismatched StdCall signature can terminate the process. Probe with no card write first.
/// The PIN is sent only by read and write, never by probe, because a wrong SLE4442 PIN can lock the card.
/// </summary>
[SupportedOSPlatform("windows")]
internal static class IcD600Gateway
{
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate short InitCommDelegate(short port);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate short ExitCommDelegate(int icdev);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate short InitTypeDelegate(int icdev, short type);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate short HandleOnlyDelegate(int icdev);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate short BeepDelegate(int icdev, short durationMs);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate short TransferDelegate(int icdev, short offset, short length, IntPtr buffer);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate short CheckPassDelegate(int icdev, IntPtr password);

    public static CardProbeResult Probe(CardIoRequest request)
    {
        using var session = Session.Open(request);
        var notes = new List<string>();
        var check = session.Optional<HandleOnlyDelegate>(request.Access.Exports.CheckCard);
        if (check is null)
        {
            notes.Add("找不到 CheckCard 匯出");
        }
        else
        {
            var code = check(session.Handle);
            notes.Add(code == request.Access.CheckCardPresentCode
                ? "已偵測到卡"
                : $"檢查卡片傳回 {code}");
        }

        var status = session.Optional<HandleOnlyDelegate>(request.Access.Exports.Status);
        if (status is not null)
            notes.Add($"狀態={status(session.Handle)}");

        var countName = request.Settings.CardType == CardType.Sle4442
            ? request.Access.Exports.ReadCount4442
            : request.Access.Exports.ReadCount4428;
        var count = session.Optional<HandleOnlyDelegate>(countName);
        if (count is not null)
            notes.Add($"讀取次數={count(session.Handle)}");

        TryBeep(session, request);
        return new CardProbeResult
        {
            Success = true,
            Message = $"讀卡機已回應，連接埠 {request.Settings.Port}（{request.Settings.CardType}）：{string.Join("；", notes)}。TODO(on-machine): 寫卡前請先確認 IC_D600 匯出名稱與整數寬度。"
        };
    }

    public static byte[] Read(CardIoRequest request, int offset, int length)
    {
        using var session = Session.Open(request);
        session.EnsureCard(request);
        session.PresentPassword(request);
        return session.Transfer(request, read: true, offset, length, buffer: null);
    }

    public static void Write(CardIoRequest request, int offset, byte[] data)
    {
        using var session = Session.Open(request);
        session.EnsureCard(request);
        session.PresentPassword(request);
        _ = session.Transfer(request, read: false, offset, data.Length, data);
    }

    private static void TryBeep(Session session, CardIoRequest request)
    {
        var beep = session.Optional<BeepDelegate>(request.Access.Exports.Beep);
        if (beep is null)
            return;

        var duration = (short)Math.Clamp(request.Access.BeepMilliseconds, 1, 500);
        _ = beep(session.Handle, duration);
    }

    private sealed class Session : IDisposable
    {
        private readonly IntPtr _library;
        private readonly IntPtr _dependency;
        private readonly ExitCommDelegate _exit;
        private readonly Dictionary<string, Delegate> _bound = new(StringComparer.Ordinal);
        private bool _closed;

        private Session(IntPtr library, IntPtr dependency, int handle, ExitCommDelegate exit)
        {
            _library = library;
            _dependency = dependency;
            Handle = handle;
            _exit = exit;
        }

        public int Handle { get; }

        public static Session Open(CardIoRequest request)
        {
            var dependency = IntPtr.Zero;
            var library = IntPtr.Zero;
            try
            {
                var dependencyPath = Path.Combine(request.BaseDirectory, CardReaderFiles.DependencyFileName);
                if (File.Exists(dependencyPath))
                    dependency = WindowsNative.LoadOrThrow(dependencyPath);

                var libraryPath = Path.Combine(request.BaseDirectory, CardReaderFiles.LibraryFileName);
                if (!File.Exists(libraryPath))
                    throw new CardReaderException($"在 {libraryPath} 找不到 IC_D600.dll。");

                library = WindowsNative.LoadOrThrow(libraryPath);
                var init = Bind<InitCommDelegate>(library, request.Access.Exports.InitComm);
                var exit = Bind<ExitCommDelegate>(library, request.Access.Exports.ExitComm);
                var code = init(checked((short)request.Settings.Port));
                var handle = InterpretInit(request, code);
                var session = new Session(library, dependency, handle, exit);
                library = IntPtr.Zero;
                dependency = IntPtr.Zero;
                try
                {
                    session.Prepare(request);
                    return session;
                }
                catch
                {
                    session.Dispose();
                    throw;
                }
            }
            finally
            {
                if (library != IntPtr.Zero)
                    _ = WindowsNative.FreeLibrary(library);
                if (dependency != IntPtr.Zero)
                    _ = WindowsNative.FreeLibrary(dependency);
            }
        }

        public void EnsureCard(CardIoRequest request)
        {
            var check = Optional<HandleOnlyDelegate>(request.Access.Exports.CheckCard);
            if (check is null)
                return;

            var code = check(Handle);
            if (code != request.Access.CheckCardPresentCode)
            {
                throw new CardReaderException(
                    $"沒有卡片，或檢查卡片傳回 {code}。此櫃檯設定的「有卡」代碼是 {request.Access.CheckCardPresentCode}。",
                    code);
            }
        }

        public void PresentPassword(CardIoRequest request)
        {
            var password = CardCodec.ParsePassword(request.Settings.CardType, request.Access.PasswordHex);
            if (password is null)
                return;

            var export = request.Settings.CardType == CardType.Sle4442
                ? request.Access.Exports.CheckPass4442
                : request.Access.Exports.CheckPass4428;
            var check = Required<CheckPassDelegate>(export);
            var buffer = Marshal.AllocHGlobal(password.Length + 1);
            try
            {
                Marshal.Copy(password, 0, buffer, password.Length);
                Marshal.WriteByte(buffer, password.Length, 0);
                var code = check(Handle, buffer);
                if (code != request.Access.IoSuccessCode)
                {
                    throw new CardReaderException(
                        $"卡片 PIN 被拒絕，代碼 {code}。SLE4442 密碼錯誤可能鎖卡。",
                        code);
                }
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }

        public byte[] Transfer(CardIoRequest request, bool read, int offset, int length, byte[]? buffer)
        {
            var export = request.Settings.CardType == CardType.Sle4442
                ? (read ? request.Access.Exports.Read4442 : request.Access.Exports.Write4442)
                : (read ? request.Access.Exports.Read4428 : request.Access.Exports.Write4428);
            var transfer = Required<TransferDelegate>(export);
            var native = Marshal.AllocHGlobal(length);
            try
            {
                if (read)
                {
                    for (var i = 0; i < length; i++)
                        Marshal.WriteByte(native, i, 0);
                }
                else
                {
                    Marshal.Copy(buffer!, 0, native, length);
                }

                var code = transfer(Handle, checked((short)offset), checked((short)length), native);
                if (code != request.Access.IoSuccessCode)
                {
                    throw new CardReaderException(
                        $"{(read ? "讀取" : "寫入")}失敗，代碼 {code}。TODO(on-machine): 取得 SDK 手冊後再對應這個廠商代碼。",
                        code);
                }

                if (!read)
                    return [];

                var data = new byte[length];
                Marshal.Copy(native, data, 0, length);
                return data;
            }
            finally
            {
                Marshal.FreeHGlobal(native);
            }
        }

        public T? Optional<T>(string name) where T : Delegate
        {
            if (string.IsNullOrWhiteSpace(name))
                return null;
            if (_bound.TryGetValue(name, out var existing))
                return (T)existing;

            var pointer = WindowsNative.GetProcAddress(_library, name);
            if (pointer == IntPtr.Zero)
                return null;

            var bound = Marshal.GetDelegateForFunctionPointer<T>(pointer);
            _bound[name] = bound;
            return bound;
        }

        public void Dispose()
        {
            if (_closed)
                return;
            _closed = true;
            try
            {
                _ = _exit(Handle);
            }
            finally
            {
                _ = WindowsNative.FreeLibrary(_library);
                if (_dependency != IntPtr.Zero)
                    _ = WindowsNative.FreeLibrary(_dependency);
            }
        }

        private void Prepare(CardIoRequest request)
        {
            if (request.Settings.InitType is int type)
            {
                var initType = Required<InitTypeDelegate>(request.Access.Exports.InitType);
                var code = initType(Handle, checked((short)type));
                if (code != request.Access.IoSuccessCode)
                    throw new CardReaderException($"IC_InitType({type}) 失敗，代碼 {code}。", code);
            }

            var chooseName = request.Settings.CardType == CardType.Sle4442
                ? request.Access.Exports.Choose4442
                : request.Access.Exports.Choose4428;
            var choose = Required<HandleOnlyDelegate>(chooseName);
            var chosen = choose(Handle);
            if (chosen != request.Access.IoSuccessCode)
                throw new CardReaderException($"選擇 {request.Settings.CardType} 失敗，代碼 {chosen}。", chosen);
        }

        private T Required<T>(string name) where T : Delegate
        {
            var bound = Optional<T>(name);
            if (bound is null)
            {
                throw new CardReaderException(
                    $"IC_D600.dll 沒有匯出 {name}。請用 dumpbin /exports IC_D600.dll 核對後，更新 LabelPrint:Card:Exports。");
            }

            return bound;
        }

        private static T Bind<T>(IntPtr library, string name) where T : Delegate
        {
            var pointer = WindowsNative.GetProcAddress(library, name);
            if (pointer == IntPtr.Zero)
            {
                throw new CardReaderException(
                    $"IC_D600.dll 沒有匯出 {name}。請用 dumpbin /exports IC_D600.dll 核對後，更新 LabelPrint:Card:Exports。");
            }

            return Marshal.GetDelegateForFunctionPointer<T>(pointer);
        }

        private static int InterpretInit(CardIoRequest request, short code)
        {
            if (request.Access.InitReturnsHandle)
            {
                if (code < 0)
                    throw new CardReaderException($"IC_InitComm 失敗，代碼 {code}。", code);
                return code;
            }

            if (code != request.Access.IoSuccessCode)
                throw new CardReaderException($"IC_InitComm 失敗，代碼 {code}。", code);
            return 0;
        }
    }
}
