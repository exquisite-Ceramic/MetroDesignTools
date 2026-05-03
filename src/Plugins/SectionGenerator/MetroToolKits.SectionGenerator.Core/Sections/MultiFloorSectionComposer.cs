using MetroToolKits.Foundation.Building.Elements;
using MetroToolKits.Foundation.Core.Geometry;

namespace MetroToolKits.SectionGenerator.Core.Sections;

/// <summary>
/// 多楼层剖面组合器
/// 对每层应用 Transform3D 对齐，按楼层标高累加 Z 坐标，合并各层 SectionGeometryData
/// </summary>
public sealed class MultiFloorSectionComposer
{
    private readonly SectionComposer _singleFloorComposer;
    private readonly FloorStackLayoutPlanner _stackLayoutPlanner;

    public MultiFloorSectionComposer(SectionComposer singleFloorComposer)
        : this(singleFloorComposer, new FloorStackLayoutPlanner())
    {
    }

    public MultiFloorSectionComposer(
        SectionComposer singleFloorComposer,
        FloorStackLayoutPlanner stackLayoutPlanner)
    {
        _singleFloorComposer = singleFloorComposer;
        _stackLayoutPlanner = stackLayoutPlanner;
    }

    /// <summary>
    /// 生成多楼层剖面数据
    /// </summary>
    /// <param name="executionContexts">每层已解析的执行上下文</param>
    /// <param name="viewDirection">视图方向</param>
    /// <param name="floorElements">每层的构件列表，key = 楼层名称</param>
    /// <param name="floors">楼层配置列表（按楼层顺序）</param>
    /// <param name="boundaryPolicy">楼层边界板堆叠策略；DrawAll 保留旧行为，可用于回退。</param>
    public MultiFloorSectionData Generate(
        IReadOnlyDictionary<string, FloorExecutionContext> executionContexts,
        Vector3D viewDirection,
        IReadOnlyDictionary<string, IReadOnlyList<BuildingElement>> floorElements,
        IReadOnlyList<FloorConfig> floors,
        SightLineComposerOptions? sightLineOptions = null,
        FloorStackBoundaryPolicy boundaryPolicy = FloorStackBoundaryPolicy.ShareInteriorBoundaries)
    {
        var floorRecognitionData = floors.ToDictionary(
            floor => floor.Name,
            floor => new SectionFloorRecognitionData
            {
                Floor = floor,
                CutElements = floorElements.TryGetValue(floor.Name, out var list)
                    ? list
                    : Array.Empty<BuildingElement>()
            },
            StringComparer.OrdinalIgnoreCase);

        return Generate(executionContexts, viewDirection, floorRecognitionData, floors, sightLineOptions, boundaryPolicy);
    }

    public MultiFloorSectionData Generate(
        IReadOnlyDictionary<string, FloorExecutionContext> executionContexts,
        Vector3D viewDirection,
        IReadOnlyDictionary<string, SectionFloorRecognitionData> floorRecognitionData,
        IReadOnlyList<FloorConfig> floors,
        SightLineComposerOptions? sightLineOptions = null,
        FloorStackBoundaryPolicy boundaryPolicy = FloorStackBoundaryPolicy.ShareInteriorBoundaries)
    {
        var floorDataList = new List<SectionGeometryData>();
        var layoutPlan = _stackLayoutPlanner.Create(floors, boundaryPolicy);
        double cumulativeElevation = 0;

        foreach (var plannedItem in layoutPlan.Items)
        {
            var floor = plannedItem.Floor;
            var layoutItem = plannedItem with { BaseElevation = cumulativeElevation };
            if (executionContexts.TryGetValue(floor.Name, out var context) &&
                context.CanParticipate &&
                context.SectionLine.HasValue)
            {
                var recognitionData = floorRecognitionData.TryGetValue(floor.Name, out var data)
                    ? data
                    : new SectionFloorRecognitionData { Floor = floor };

                var floorData = _singleFloorComposer.Generate(
                    context.SectionLine.Value,
                    viewDirection,
                    recognitionData,
                    baseElevation: cumulativeElevation,
                    sightLineOptions: sightLineOptions,
                    stackLayoutItem: layoutItem);

                floorDataList.Add(floorData);

                cumulativeElevation += ComputeTotalFloorHeight(floorData, floor, layoutItem);
                continue;
            }

            cumulativeElevation += ComputeFallbackFloorHeight(floor, layoutItem);
        }

        return new MultiFloorSectionData
        {
            Floors = floorDataList,
            TotalHeight = cumulativeElevation
        };
    }

    private static double ComputeTotalFloorHeight(
        SectionGeometryData floorData,
        FloorConfig fallbackFloor,
        FloorStackLayoutItem layoutItem)
    {
        if (floorData.VerticalProfile == null)
        {
            return ComputeFallbackFloorHeight(fallbackFloor, layoutItem);
        }

        var profile = floorData.VerticalProfile;
        var bottomThickness = profile.GetBottomStructuralTop(0) - profile.GetBottomStructuralBottom(0);
        var topThickness = profile.GetTopStructuralTop(0) - profile.GetTopStructuralBottom(0);
        // Height contribution intentionally follows existing structural-thickness behavior.
        return (layoutItem.BottomBoundary.CountHeight ? bottomThickness : 0)
               + floorData.FloorHeight
               + (layoutItem.TopBoundary.CountHeight ? topThickness : 0);
    }

    private static double ComputeFallbackFloorHeight(FloorConfig floor, FloorStackLayoutItem layoutItem)
        => (layoutItem.BottomBoundary.CountHeight ? floor.BottomSlabThickness : 0)
           + floor.Height
           + (layoutItem.TopBoundary.CountHeight ? floor.TopSlabThickness : 0);

}

/// <summary>
/// 多楼层剖面数据
/// </summary>
public sealed class MultiFloorSectionData
{
    /// <summary>各楼层剖面数据（按楼层顺序）</summary>
    public IReadOnlyList<SectionGeometryData> Floors { get; init; } = Array.Empty<SectionGeometryData>();

    /// <summary>总高度（所有楼层累加）</summary>
    public double TotalHeight { get; init; }

    /// <summary>所有楼层的剖切线（合并）</summary>
    public IEnumerable<Line3D> AllCutLines => Floors.SelectMany(f => f.AllCutLines);

    /// <summary>所有楼层的看线（合并）</summary>
    public IEnumerable<Line3D> AllSightLines => Floors.SelectMany(f => f.AllSightLines);
}
