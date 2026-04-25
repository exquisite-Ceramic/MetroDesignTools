using MetroToolKits.SectionGenerator.Contracts.Common;

namespace MetroToolKits.SectionGenerator.Contracts.Floors;

public sealed class SaveFloorConfigRequestDto
{
    public string AlignmentBaseFloorName { get; init; } = string.Empty;

    public bool GlobalSlopeEnabled { get; init; }

    public double GlobalSlopePercent { get; init; }

    public string GlobalSlopeTarget { get; init; } = string.Empty;

    public bool GlobalTopSlopeEnabled { get; init; }

    public double GlobalTopSlopePercent { get; init; }

    public string GlobalTopSlopeTarget { get; init; } = string.Empty;

    public bool GlobalBottomSlopeEnabled { get; init; }

    public double GlobalBottomSlopePercent { get; init; }

    public string GlobalBottomSlopeTarget { get; init; } = string.Empty;

    public IReadOnlyList<FloorConfigEditDto> Floors { get; init; } = Array.Empty<FloorConfigEditDto>();
}

public sealed class FloorConfigEditDto
{
    public string Name { get; init; } = string.Empty;

    public double Height { get; init; }

    public double FinishThickness { get; init; }

    public double BottomSlabThickness { get; init; }

    public double TopSlabThickness { get; init; }

    public bool LegacySlopeEnabled { get; init; }

    public double LegacySlopePercent { get; init; }

    public string LegacySlopeTarget { get; init; } = string.Empty;

    public IReadOnlyList<Point3DDto> AlignmentPoints { get; init; } = Array.Empty<Point3DDto>();

    public ScopeBoundsDto? ScopeBounds { get; init; }

    public BoundarySlabEditDto TopBoundarySlab { get; init; } = new();

    public BoundarySlabEditDto BottomBoundarySlab { get; init; } = new();
}

public sealed class BoundarySlabEditDto
{
    public string TemplateId { get; init; } = string.Empty;

    public bool SlopeEnabled { get; init; }

    public double SlopePercent { get; init; }

    public string SlopeTarget { get; init; } = string.Empty;
}
