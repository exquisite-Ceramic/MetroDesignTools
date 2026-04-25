namespace MetroToolKits.SectionGenerator.Contracts.Workbench;

public sealed class SectionWorkbenchSnapshotDto
{
    public DrawingStatusDto Drawing { get; init; } = new();

    public FloorConfigStatusDto FloorConfig { get; init; } = new();

    public ElementReadinessDto ElementReadiness { get; init; } = new();

    public ExistingSectionStatusDto ExistingSections { get; init; } = new();

    public RecommendedActionDto RecommendedAction { get; init; } = new();

    public IReadOnlyList<ValidationIssueDto> Issues { get; init; } = Array.Empty<ValidationIssueDto>();
}

public sealed class DrawingStatusDto
{
    public string DrawingDisplayName { get; init; } = string.Empty;

    public DrawingConfigSourceDto StorageSource { get; init; }

    public bool IsCurrentDrawingSaved { get; init; }

    public bool HasPersistedConfig { get; init; }
}

public sealed class FloorConfigStatusDto
{
    public bool CanGenerate { get; init; }

    public int FloorCount { get; init; }

    public int MissingRequirementCount { get; init; }

    public string AlignmentBaseFloorName { get; init; } = string.Empty;

    public string SummaryText { get; init; } = string.Empty;
}

public sealed class ElementReadinessDto
{
    public bool HasRecognizableElements { get; init; }

    public int TotalRecognizableElementCount { get; init; }

    public int RecognizableWallCount { get; init; }

    public int RecognizableColumnCount { get; init; }

    public int RecognizableSlabCount { get; init; }

    public int TemplatedWallCount { get; init; }

    public int TemplatedSlabCount { get; init; }

    public int LegacyWallCount { get; init; }

    public int LegacySlabCount { get; init; }

    public string SummaryText { get; init; } = string.Empty;
}

public sealed class ExistingSectionStatusDto
{
    public bool CanInspect { get; init; }

    public int TotalCount { get; init; }

    public int UpToDateCount { get; init; }

    public int OutdatedCount { get; init; }

    public int UnknownCount { get; init; }

    public int PartialCount { get; init; }

    public string? ErrorMessage { get; init; }

    public string SummaryText { get; init; } = string.Empty;
}

public sealed class RecommendedActionDto
{
    public RecommendedActionKindDto Kind { get; init; }

    public string CommandTag { get; init; } = string.Empty;

    public string ButtonText { get; init; } = string.Empty;

    public string Message { get; init; } = string.Empty;
}

public sealed class ValidationIssueDto
{
    public string Code { get; init; } = string.Empty;

    public string Message { get; init; } = string.Empty;

    public ValidationSeverityDto Severity { get; init; }

    public string? Target { get; init; }
}

public enum DrawingConfigSourceDto
{
    EmbeddedDwg = 0,
    TransientUnsavedDrawing = 1,
    Missing = 2
}

public enum RecommendedActionKindDto
{
    ConfigureFloors = 0,
    OpenLayerMapping = 1,
    ReviewExistingSections = 2,
    GenerateSection = 3
}

public enum ValidationSeverityDto
{
    Info = 0,
    Warning = 1,
    Error = 2
}
