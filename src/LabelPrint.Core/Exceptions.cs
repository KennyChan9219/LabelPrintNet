namespace LabelPrint;

/// <summary>
/// Raised when a desk operation needs the MIS database and no real connection string is configured.
/// </summary>
public sealed class MisNotConfiguredException : InvalidOperationException
{
    public MisNotConfiguredException(string message)
        : base(message)
    {
    }
}

/// <summary>
/// Raised when the smart-card reader cannot complete a call. <see cref="NativeCode"/> is the
/// vendor return value when the DLL was actually invoked.
/// </summary>
public sealed class CardReaderException : Exception
{
    public CardReaderException(string message, int? nativeCode = null, Exception? inner = null)
        : base(message, inner)
    {
        NativeCode = nativeCode;
    }

    public int? NativeCode { get; }
}
