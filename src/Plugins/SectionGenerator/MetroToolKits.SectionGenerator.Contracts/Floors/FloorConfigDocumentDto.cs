using MetroToolKits.SectionGenerator.Contracts.Workbench;

namespace MetroToolKits.SectionGenerator.Contracts.Floors;

public sealed class FloorConfigDocumentDto
{
    public DrawingStatusDto Drawing { get; init; } = new();

    public GlobalSlopeDto GlobalSlope { get; init; } = new();

    public string SelectedBaseFloorName { get; init; } = string.Empty;

    public string ConfigSourceStatusText { get; init; } = string.Empty;

    public IReadOnlyList<ValidationIssueDto> Issues { get; init; } = Array.Empty<ValidationIssueDto>();

    public IReadOnlyList<FloorSummaryDto> FloorSummaries { get; init; } = Array.Empty<FloorSummaryDto>();

    public IReadOnlyList<FloorDetailDto> FloorDetails { get; init; } = Array.Empty<FloorDetailDto>();
}

public sealed class FloorSummaryDto
{
    public string FloorName { get; init; } = string.Empty;

    public string StackRoleText { get; init; } = string.Empty;

    public bool IsBaseFloor { get; init; }

    public bool HasBlockingIssues { get; init; }

    public string StatusText { get; init; } = string.Empty;
}

public sealed class FloorDetailDto
{
    public string FloorName { get; init; } = string.Empty;

    public bool IsBaseFloor { get; init; }

    public FloorAlignmentDto Alignment { get; init; } = new();

    public FloorScopeDto Scope { get; init; } = new();

    public BoundarySlabDto TopBoundarySlab { get; init; } = new();

    public BoundarySlabDto BottomBoundarySlab { get; init; } = new();

    public FloorSlopeDto TopSlope { get; init; } = new();

    public FloorSlopeDto BottomSlope { get; init; } = new();

    public IReadOnlyList<ValidationIssueDto> MissingRequirements { get; init; } = Array.Empty<ValidationIssueDto>();
}

public sealed class BoundarySlabDto
{
    public string TemplateId { get; init; } = string.Empty;

    public string TemplateName { get; init; } = string.Empty;

    public bool TemplateExists { get; init; }

    public double? StructuralThickness { get; init; }

    public double? FinishThickness { get; init; }

    public string DisplayText { get; init; } = string.Empty;
}

public sealed class FloorAlignmentDto
{
    public int PointCount { get; init; }

    public bool IsValid { get; init; }

    public string StatusText { get; init; } = string.Empty;
}

public sealed class FloorScopeDto
{
    public bool HasValue { get; init; }

    public bool IsValid { get; init; }

    public ScopeBoundsDto? Bounds { get; init; }

    public string StatusText { get; init; } = string.Empty;
}

public sealed class ScopeBoundsDto
{
    public double MinX { get; init; }

    public double MinY { get; init; }

    public double MaxX { get; init; }

    public double MaxY { get; init; }
}

public sealed class GlobalSlopeDto
{
    public bool Enabled { get; init; }

    public double SlopePercent { get; init; }

    public string Target { get; init; } = string.Empty;

    public string StatusText { get; init; } = string.Empty;
}

public sealed class FloorSlopeDto
{
    public bool Enabled { get; init; }

    public double SlopePercent { get; init; }

    public string Target { get; init; } = string.Empty;

    public string StatusText { get; init; } = string.Empty;
}
