namespace MetroToolKits.SectionGenerator.Core.Sections;

public sealed record FloorStackLayoutPlan
{
    public FloorStackBoundaryPolicy Policy { get; init; } = FloorStackBoundaryPolicy.DrawAll;

    public IReadOnlyList<FloorStackLayoutItem> Items { get; init; } = Array.Empty<FloorStackLayoutItem>();
}
