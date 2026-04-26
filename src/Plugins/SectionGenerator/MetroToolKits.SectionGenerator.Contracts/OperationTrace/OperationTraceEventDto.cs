namespace MetroToolKits.SectionGenerator.Contracts.OperationTrace;

public sealed class OperationTraceEventDto
{
    public string EventId { get; init; } = string.Empty;

    public string SessionId { get; init; } = string.Empty;

    public DateTimeOffset Timestamp { get; init; }

    public string Area { get; init; } = string.Empty;

    public string Action { get; init; } = string.Empty;

    public string Target { get; init; } = string.Empty;

    public string Result { get; init; } = string.Empty;

    public string ErrorCode { get; init; } = string.Empty;

    public string Message { get; init; } = string.Empty;

    public IReadOnlyDictionary<string, string> Properties { get; init; } = new Dictionary<string, string>();
}
