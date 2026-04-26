using System.Linq;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using MetroToolKits.SectionGenerator.App.Abstractions;
using MetroToolKits.SectionGenerator.Contracts.OperationTrace;

namespace MetroToolKits.SectionGenerator.Infrastructure.Diagnostics;

public sealed class JsonOperationTraceRecorder : IOperationTraceRecorder
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false
    };

    private readonly object _syncRoot = new();
    private readonly ILogger<JsonOperationTraceRecorder> _logger;
    private readonly string _traceDirectory;
    private StreamWriter? _writer;
    private string _writerDateKey = string.Empty;
    private string _currentSessionId = string.Empty;

    public JsonOperationTraceRecorder(ILogger<JsonOperationTraceRecorder> logger)
    {
        _logger = logger;
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        _traceDirectory = Path.Combine(localAppData, "MetroToolKits", "OperationTrace");
    }

    public string StartSession(string drawingName)
    {
        lock (_syncRoot)
        {
            _currentSessionId = Guid.NewGuid().ToString("N");
            return _currentSessionId;
        }
    }

    public void Record(OperationTraceEventDto operationEvent)
    {
        try
        {
            lock (_syncRoot)
            {
                EnsureWriter();

                var normalizedEvent = Normalize(operationEvent);
                var json = JsonSerializer.Serialize(normalizedEvent, JsonOptions);
                _writer!.WriteLine(json);
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "OperationTrace 写入失败");
        }
    }

    public void EndSession(string sessionId)
    {
        lock (_syncRoot)
        {
            if (string.Equals(_currentSessionId, sessionId, StringComparison.Ordinal))
            {
                _currentSessionId = string.Empty;
            }

            TryFlushCore();
        }
    }

    public void Flush()
    {
        lock (_syncRoot)
        {
            TryFlushCore();
        }
    }

    private OperationTraceEventDto Normalize(OperationTraceEventDto operationEvent)
    {
        var properties = operationEvent.Properties
            .Where(pair => !string.IsNullOrWhiteSpace(pair.Key))
            .ToDictionary(
                pair => pair.Key,
                pair => pair.Value ?? string.Empty,
                StringComparer.Ordinal);

        return new OperationTraceEventDto
        {
            EventId = string.IsNullOrWhiteSpace(operationEvent.EventId)
                ? Guid.NewGuid().ToString("N")
                : operationEvent.EventId,
            SessionId = string.IsNullOrWhiteSpace(operationEvent.SessionId)
                ? _currentSessionId
                : operationEvent.SessionId,
            Timestamp = operationEvent.Timestamp == default
                ? DateTimeOffset.UtcNow
                : operationEvent.Timestamp,
            Area = operationEvent.Area ?? string.Empty,
            Action = operationEvent.Action ?? string.Empty,
            Target = operationEvent.Target ?? string.Empty,
            Result = operationEvent.Result ?? string.Empty,
            ErrorCode = operationEvent.ErrorCode ?? string.Empty,
            Message = operationEvent.Message ?? string.Empty,
            Properties = properties
        };
    }

    private void EnsureWriter()
    {
        var dateKey = DateTimeOffset.Now.ToString("yyyyMMdd");
        if (_writer != null && string.Equals(_writerDateKey, dateKey, StringComparison.Ordinal))
        {
            return;
        }

        _writer?.Dispose();
        Directory.CreateDirectory(_traceDirectory);

        var filePath = Path.Combine(_traceDirectory, $"operation-trace-{dateKey}.jsonl");
        var stream = new FileStream(filePath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
        _writer = new StreamWriter(stream);
        _writerDateKey = dateKey;
    }

    private void TryFlushCore()
    {
        try
        {
            _writer?.Flush();
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "OperationTrace Flush 失败");
        }
    }
}
