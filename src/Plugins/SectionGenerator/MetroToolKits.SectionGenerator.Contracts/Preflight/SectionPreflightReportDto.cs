namespace MetroToolKits.SectionGenerator.Contracts.Preflight;

public sealed class SectionPreflightReportDto
{
    public string DrawingDisplayName { get; init; } = string.Empty;

    public string DrawingStatusText { get; init; } = string.Empty;

    public string ConfigSourceText { get; init; } = string.Empty;

    public bool IsCurrentDrawingSaved { get; init; }

    public bool HasPersistedConfig { get; init; }

    public int FloorCount { get; init; }

    public string AlignmentBaseFloorName { get; init; } = string.Empty;

    public bool CanGenerate { get; init; }

    public int BlockingCount { get; init; }

    public int WarningCount { get; init; }

    public int InfoCount { get; init; }

    public string SummaryText { get; init; } = string.Empty;

    public IReadOnlyList<SectionPreflightCheckItemDto> Checks { get; init; } = Array.Empty<SectionPreflightCheckItemDto>();

    public IReadOnlyList<SectionPreflightFloorStatusDto> Floors { get; init; } = Array.Empty<SectionPreflightFloorStatusDto>();
}

public sealed class SectionPreflightCheckItemDto
{
    public string Key { get; init; } = string.Empty;

    public string Title { get; init; } = string.Empty;

    public SectionPreflightSeverityDto Severity { get; init; }

    public string Summary { get; init; } = string.Empty;

    public string SuggestedActionText { get; init; } = string.Empty;

    public string RelatedObjectName { get; init; } = string.Empty;

    public string SuggestedCommandTag { get; init; } = string.Empty;

    public SectionPreflightActionTargetDto ActionTarget { get; init; }

    public string? FloorName { get; init; }
}

public sealed class SectionPreflightFloorStatusDto
{
    public string FloorName { get; init; } = string.Empty;

    public bool IsBaseFloor { get; init; }

    public bool WillBeSkipped { get; init; }

    public string AlignmentStatusText { get; init; } = string.Empty;

    public string ScopeStatusText { get; init; } = string.Empty;

    public string BoundaryTemplateStatusText { get; init; } = string.Empty;

    public string SkipReasonText { get; init; } = string.Empty;
}

public enum SectionPreflightSeverityDto
{
    Passed = 0,
    Info = 1,
    Warning = 2,
    Blocking = 3
}

public enum SectionPreflightActionTargetDto
{
    None = 0,
    FloorConfig = 1,
    LayerMapping = 2
}
