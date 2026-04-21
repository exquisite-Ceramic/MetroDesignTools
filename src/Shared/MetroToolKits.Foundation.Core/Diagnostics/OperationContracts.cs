namespace MetroToolKits.Foundation.Core.Diagnostics;

/// <summary>
/// 统一操作状态。
/// </summary>
public enum OperationStatus
{
    Success = 0,
    PartialSuccess = 1,
    Failed = 2
}

/// <summary>
/// 失败分类。
/// </summary>
public enum FailureCategory
{
    UserInput = 0,
    ApplicationFlow = 1,
    DomainBusiness = 2,
    Infrastructure = 3,
    SystemInternal = 4
}

/// <summary>
/// 流程阶段。
/// </summary>
public enum PipelineStage
{
    InputValidation = 0,
    FloorConfigLoad = 1,
    ElementRecognition = 2,
    SectionComposition = 3,
    DrawingOutput = 4,
    SnapshotPersist = 5
}

/// <summary>
/// 诊断级别。
/// </summary>
public enum DiagnosticLevel
{
    Info = 0,
    Warning = 1,
    Error = 2
}

/// <summary>
/// 非致命诊断信息。
/// </summary>
public sealed record class OperationDiagnostic
{
    public DiagnosticLevel Level { get; init; }
    public string Code { get; init; } = string.Empty;
    public PipelineStage Stage { get; init; }
    public string Module { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public string? TargetHandle { get; init; }
    public string? Suggestion { get; init; }
    public IReadOnlyDictionary<string, string?> Metadata { get; init; } =
        new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
}

/// <summary>
/// 致命失败信息。
/// </summary>
public sealed record class OperationFailure
{
    public string Code { get; init; } = string.Empty;
    public FailureCategory Category { get; init; }
    public PipelineStage Stage { get; init; }
    public string Module { get; init; } = string.Empty;
    public string UserMessage { get; init; } = string.Empty;
    public string? TechnicalMessage { get; init; }
    public Exception? InnerException { get; init; }
}

/// <summary>
/// 用例结果基类。
/// </summary>
public abstract class OperationResult
{
    public OperationStatus Status { get; init; } = OperationStatus.Success;
    public OperationFailure? Failure { get; init; }
    public IReadOnlyList<OperationDiagnostic> Diagnostics { get; init; } = Array.Empty<OperationDiagnostic>();
    public PipelineStage? FailedStage { get; init; }
}
