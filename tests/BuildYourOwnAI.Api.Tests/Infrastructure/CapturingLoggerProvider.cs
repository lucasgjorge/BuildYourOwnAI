using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace BuildYourOwnAI.Api.Tests.Infrastructure;

public sealed record CapturedLog(string Category, LogLevel Level, string Message, IReadOnlyDictionary<string, object?> Properties);

public sealed class CapturingLoggerProvider : ILoggerProvider
{
    public ConcurrentQueue<CapturedLog> Entries { get; } = new();

    public ILogger CreateLogger(string categoryName) => new Logger(categoryName, Entries);

    public void Dispose() { }

    private sealed class Logger(string category, ConcurrentQueue<CapturedLog> entries) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            var props = state is IEnumerable<KeyValuePair<string, object?>> kvs
                ? kvs.ToDictionary(kv => kv.Key, kv => kv.Value)
                : new Dictionary<string, object?>();
            var message = formatter(state, exception) + (exception is null ? "" : " | " + exception);
            entries.Enqueue(new CapturedLog(category, logLevel, message, props));
        }
    }
}
