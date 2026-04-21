using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Text.Json;

namespace MetroToolKits.Bootstrap.Logging;

/// <summary>
/// 文件日志记录器（JSON 格式，每行一条）
/// </summary>
internal sealed class FileLogger : ILogger
{
    private readonly string _categoryName;
    private readonly FileLoggerProcessor _processor;

    public FileLogger(string categoryName, FileLoggerProcessor processor)
    {
        _categoryName = categoryName;
        _processor = processor;
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
        Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel)) return;

        var entry = new Dictionary<string, object?>
        {
            ["time"] = DateTime.Now.ToString("O"),
            ["level"] = logLevel.ToString().ToUpperInvariant(),
            ["category"] = _categoryName,
            ["msg"] = formatter(state, exception),
            ["exception"] = exception?.ToString()
        };

        if (state is IEnumerable<KeyValuePair<string, object?>> structuredState)
        {
            foreach (var pair in structuredState)
            {
                if (string.Equals(pair.Key, "{OriginalFormat}", StringComparison.Ordinal))
                    continue;

                entry[pair.Key] = pair.Value;
            }
        }

        _processor.Enqueue(JsonSerializer.Serialize(entry));
    }
}

/// <summary>
/// 文件日志处理器（后台队列写入）
/// </summary>
public sealed class FileLoggerProcessor : IDisposable
{
    private readonly string _filePath;
    private readonly BlockingCollection<string> _queue = new(1024);
    private readonly Thread _writerThread;
    private volatile bool _disposed;

    public FileLoggerProcessor(string filePath)
    {
        _filePath = filePath;
        var dir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        _writerThread = new Thread(ProcessQueue) { IsBackground = true };
        _writerThread.Start();
    }

    public void Enqueue(string logLine) => _queue.TryAdd(logLine);

    private void ProcessQueue()
    {
        foreach (var line in _queue.GetConsumingEnumerable())
        {
            if (_disposed) break;
            try
            {
                File.AppendAllText(_filePath, line + Environment.NewLine);
            }
            catch { /* 忽略文件写入错误 */ }
        }
    }

    public void Dispose()
    {
        _disposed = true;
        _queue.CompleteAdding();
        _writerThread.Join(TimeSpan.FromSeconds(2));
        _queue.Dispose();
    }
}

/// <summary>
/// FileLogger 提供者
/// </summary>
public sealed class FileLoggerProvider : ILoggerProvider
{
    private readonly FileLoggerProcessor _processor;

    public FileLoggerProvider(string filePath)
    {
        _processor = new FileLoggerProcessor(filePath);
    }

    public ILogger CreateLogger(string categoryName)
        => new FileLogger(categoryName, _processor);

    public void Dispose() => _processor.Dispose();
}
