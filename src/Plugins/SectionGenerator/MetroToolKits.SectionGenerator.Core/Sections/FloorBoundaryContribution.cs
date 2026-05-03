namespace MetroToolKits.SectionGenerator.Core.Sections;

public sealed record FloorBoundaryContribution(
    FloorBoundaryKind Kind,
    bool DrawLines,
    bool DrawHatch,
    bool CountHeight)
{
    public static FloorBoundaryContribution Include(FloorBoundaryKind kind)
        => new(kind, DrawLines: true, DrawHatch: true, CountHeight: true);

    public static FloorBoundaryContribution Suppress(FloorBoundaryKind kind)
        => new(kind, DrawLines: false, DrawHatch: false, CountHeight: false);
}
