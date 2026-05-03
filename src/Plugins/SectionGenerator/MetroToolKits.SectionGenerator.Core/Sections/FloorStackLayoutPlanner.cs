namespace MetroToolKits.SectionGenerator.Core.Sections;

public sealed class FloorStackLayoutPlanner
{
    public FloorStackLayoutPlan Create(
        IReadOnlyList<FloorConfig> floors,
        FloorStackBoundaryPolicy policy)
    {
        var items = new List<FloorStackLayoutItem>(floors.Count);
        var baseElevation = 0d;

        for (var i = 0; i < floors.Count; i++)
        {
            var floor = floors[i];
            var bottomBoundary = CreateBoundaryContribution(policy, FloorBoundaryKind.Bottom, i, floors.Count);
            var topBoundary = CreateBoundaryContribution(policy, FloorBoundaryKind.Top, i, floors.Count);

            items.Add(new FloorStackLayoutItem
            {
                Floor = floor,
                FloorIndex = i,
                FloorCount = floors.Count,
                BaseElevation = baseElevation,
                BottomBoundary = bottomBoundary,
                TopBoundary = topBoundary
            });

            baseElevation += ComputeFallbackFloorHeight(floor, bottomBoundary, topBoundary);
        }

        return new FloorStackLayoutPlan
        {
            Policy = policy,
            Items = items
        };
    }

    private static FloorBoundaryContribution CreateBoundaryContribution(
        FloorStackBoundaryPolicy policy,
        FloorBoundaryKind kind,
        int floorIndex,
        int floorCount)
    {
        if (policy == FloorStackBoundaryPolicy.ShareInteriorBoundaries &&
            kind == FloorBoundaryKind.Bottom &&
            floorIndex > 0 &&
            floorCount > 1)
        {
            return FloorBoundaryContribution.Suppress(kind);
        }

        return FloorBoundaryContribution.Include(kind);
    }

    private static double ComputeFallbackFloorHeight(
        FloorConfig floor,
        FloorBoundaryContribution bottomBoundary,
        FloorBoundaryContribution topBoundary)
        => (bottomBoundary.CountHeight ? floor.BottomSlabThickness : 0)
           + floor.Height
           + (topBoundary.CountHeight ? floor.TopSlabThickness : 0);
}
