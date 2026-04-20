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

    public MultiFloorSectionComposer(SectionComposer singleFloorComposer)
    {
        _singleFloorComposer = singleFloorComposer;
    }

    /// <summary>
    /// 生成多楼层剖面数据
    /// </summary>
    /// <param name="sectionLine">剖切线（平面坐标）</param>
    /// <param name="viewDirection">视图方向</param>
    /// <param name="floorElements">每层的构件列表，key = 楼层名称</param>
    /// <param name="floors">楼层配置列表（按楼层顺序）</param>
    public MultiFloorSectionData Generate(
        Line3D sectionLine,
        Vector3D viewDirection,
        IReadOnlyDictionary<string, IReadOnlyList<BuildingElement>> floorElements,
        IReadOnlyList<FloorConfig> floors)
    {
        var floorDataList = new List<SectionGeometryData>();
        double cumulativeElevation = 0;

        foreach (var floor in floors)
        {
            // 1. 获取该层构件（若无则用空列表）
            var elements = floorElements.TryGetValue(floor.Name, out var list)
                ? list
                : Array.Empty<BuildingElement>();

            // 2. 计算对齐变换（若配置了三点对齐）
            var alignedSectionLine = FloorSectionLineTransformer.ApplyAlignment(sectionLine, floor);

            // 3. 生成单层剖面数据（传入累计标高）
            var floorData = _singleFloorComposer.Generate(
                alignedSectionLine,
                viewDirection,
                elements,
                floor,
                baseElevation: cumulativeElevation);

            floorDataList.Add(floorData);

            // 4. 累加标高（底板厚 + 层高 + 顶板厚）
            cumulativeElevation += floor.BottomSlabThickness + floor.Height + floor.TopSlabThickness;
        }

        return new MultiFloorSectionData
        {
            Floors = floorDataList,
            TotalHeight = cumulativeElevation
        };
    }

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
