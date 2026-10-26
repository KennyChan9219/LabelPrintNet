using Microsoft.Extensions.Logging;

namespace LabelPrint;

internal static class FileLog
{
    public static string Path { get; } = System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "LabelPrint",
        "logs",
        "labelprint.log");
}

internal sealed class FileLoggerProvider : ILoggerProvider
{
    private readonly StreamWriter _writer;
    private readonly object _gate = new();

    public FileLoggerProvider()
    {
        var directory = System.IO.Path.GetDirectoryName(FileLog.Path);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        _writer = new StreamWriter(new FileStream(FileLog.Path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite))
        {
            AutoFlush = true
        };
    }

    public ILogger CreateLogger(string categoryName) => new FileLogger(categoryName, _writer, _gate);

    public void Dispose() => _writer.Dispose();

    private sealed class FileLogger : ILogger
    {
        private readonly string _category;
        private readonly StreamWriter _writer;
        private readonly object _gate;

        public FileLogger(string category, StreamWriter writer, object gate)
        {
            _category = category;
            _writer = writer;
            _gate = gate;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) =>
            logLevel >= LogLevel.Information && !_category.StartsWith("Microsoft.", StringComparison.Ordinal);

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
                return;

            var message = formatter(state, exception);
            if (message.Contains("Password=", StringComparison.OrdinalIgnoreCase))
                message = "(message omitted because it contained a connection string)";

            var line = $"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss} {logLevel} {_category} {message}";
            if (exception is not null)
                line += Environment.NewLine + exception.GetType().Name + ": " + exception.Message;

            lock (_gate)
                _writer.WriteLine(line);
        }
    }
}
