namespace MetroToolKits.SectionGenerator.Core.Sections;

public sealed record FloorStackLayoutItem
{
    public required FloorConfig Floor { get; init; }

    public int FloorIndex { get; init; }

    public int FloorCount { get; init; } = 1;

    public double BaseElevation { get; init; }

    public FloorBoundaryContribution BottomBoundary { get; init; } =
        FloorBoundaryContribution.Include(FloorBoundaryKind.Bottom);

    public FloorBoundaryContribution TopBoundary { get; init; } =
        FloorBoundaryContribution.Include(FloorBoundaryKind.Top);

    public bool IsSingle => FloorCount <= 1;

    public bool IsBottomFloor => FloorIndex == 0;

    public bool IsTopFloor => FloorIndex == FloorCount - 1;

    public bool IsMiddleFloor => !IsSingle && !IsBottomFloor && !IsTopFloor;

    public static FloorStackLayoutItem CreateDrawAll(
        FloorConfig floor,
        double baseElevation = 0,
        int floorIndex = 0,
        int floorCount = 1)
        => new()
        {
            Floor = floor,
            FloorIndex = floorIndex,
            FloorCount = floorCount,
            BaseElevation = baseElevation,
            BottomBoundary = FloorBoundaryContribution.Include(FloorBoundaryKind.Bottom),
            TopBoundary = FloorBoundaryContribution.Include(FloorBoundaryKind.Top)
        };
}
