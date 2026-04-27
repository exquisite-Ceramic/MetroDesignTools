using MetroToolKits.Foundation.Core.Geometry;

namespace MetroToolKits.SectionGenerator.Core.Sections;

/// <summary>
/// 楼层对齐问题类型。
/// </summary>
public enum FloorAlignmentIssueKind
{
    None = 0,
    AlignmentBaseFloorMissing = 1,
    AlignmentBaseFloorInvalid = 2,
    AlignmentPointsMissing = 3,
    AlignmentPointsInvalid = 4
}

/// <summary>
/// 楼层对齐问题。
/// </summary>
public sealed record class FloorAlignmentIssue
{
    public FloorAlignmentIssueKind Kind { get; init; }
    public string Message { get; init; } = string.Empty;
}

/// <summary>
/// 单楼层对齐解析结果。
/// </summary>
public sealed class FloorAlignmentResult
{
    public string FloorName { get; init; } = string.Empty;
    public bool IsBaseFloor { get; init; }
    public bool CanParticipate { get; init; }
    public Line3D? SectionLine { get; init; }
    public FloorAlignmentIssue? Issue { get; init; }
}

/// <summary>
/// 楼层对齐解析总结果。
/// </summary>
public sealed class FloorAlignmentResolution
{
    public IReadOnlyDictionary<string, FloorAlignmentResult> Floors { get; init; } =
        new Dictionary<string, FloorAlignmentResult>(StringComparer.OrdinalIgnoreCase);

    public FloorAlignmentIssue? FatalIssue { get; init; }
}

/// <summary>
/// 将“基准层 3 点 + 每层 3 点”解析为每层实际参与的剖切线。
/// </summary>
public sealed class FloorAlignmentResolver
{
    public FloorAlignmentResolution Resolve(Line3D sectionLine, SectionConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        var floors = config.Floors ?? new List<FloorConfig>();
        var results = new Dictionary<string, FloorAlignmentResult>(StringComparer.OrdinalIgnoreCase);

        if (floors.Count == 0)
        {
            return new FloorAlignmentResolution { Floors = results };
        }

        if (floors.Count == 1)
        {
            var singleFloor = floors[0];
            results[singleFloor.Name] = ResolveSingleFloor(sectionLine, singleFloor);
            return new FloorAlignmentResolution { Floors = results };
        }

        if (string.IsNullOrWhiteSpace(config.AlignmentBaseFloorName))
        {
            return new FloorAlignmentResolution
            {
                Floors = results,
                FatalIssue = new FloorAlignmentIssue
                {
                    Kind = FloorAlignmentIssueKind.AlignmentBaseFloorMissing,
                    Message = "多楼层模式下未配置基准层。"
                }
            };
        }

        var baseFloor = floors.FirstOrDefault(f =>
            string.Equals(f.Name, config.AlignmentBaseFloorName, StringComparison.OrdinalIgnoreCase));

        if (baseFloor == null)
        {
            return new FloorAlignmentResolution
            {
                Floors = results,
                FatalIssue = new FloorAlignmentIssue
                {
                    Kind = FloorAlignmentIssueKind.AlignmentBaseFloorMissing,
                    Message = $"未找到名为 {config.AlignmentBaseFloorName} 的基准层。"
                }
            };
        }

        if (!FloorSectionLineTransformer.TryValidateAlignmentPoints(baseFloor.AlignmentPoints, out var baseError))
        {
            return new FloorAlignmentResolution
            {
                Floors = results,
                FatalIssue = new FloorAlignmentIssue
                {
                    Kind = FloorAlignmentIssueKind.AlignmentBaseFloorInvalid,
                    Message = $"基准层 {baseFloor.Name} 的对齐点无效: {baseError}"
                }
            };
        }

        results[baseFloor.Name] = new FloorAlignmentResult
        {
            FloorName = baseFloor.Name,
            IsBaseFloor = true,
            CanParticipate = true,
            SectionLine = sectionLine
        };

        foreach (var floor in floors.Where(f => !string.Equals(f.Name, baseFloor.Name, StringComparison.OrdinalIgnoreCase)))
        {
            if (floor.AlignmentPoints.Count == 0)
            {
                results[floor.Name] = new FloorAlignmentResult
                {
                    FloorName = floor.Name,
                    IsBaseFloor = false,
                    CanParticipate = false,
                    Issue = new FloorAlignmentIssue
                    {
                        Kind = FloorAlignmentIssueKind.AlignmentPointsMissing,
                        Message = $"楼层 {floor.Name} 缺少对齐点。"
                    }
                };
                continue;
            }

            if (!FloorSectionLineTransformer.TryValidateAlignmentPoints(floor.AlignmentPoints, out var floorError))
            {
                results[floor.Name] = new FloorAlignmentResult
                {
                    FloorName = floor.Name,
                    IsBaseFloor = false,
                    CanParticipate = false,
                    Issue = new FloorAlignmentIssue
                    {
                        Kind = FloorAlignmentIssueKind.AlignmentPointsInvalid,
                        Message = $"楼层 {floor.Name} 的对齐点无效: {floorError}"
                    }
                };
                continue;
            }

            results[floor.Name] = new FloorAlignmentResult
            {
                FloorName = floor.Name,
                IsBaseFloor = false,
                CanParticipate = true,
                SectionLine = FloorSectionLineTransformer.ApplyAlignment(
                    sectionLine,
                    baseFloor.AlignmentPoints,
                    floor.AlignmentPoints)
            };
        }

        return new FloorAlignmentResolution { Floors = results };
    }

    private static FloorAlignmentResult ResolveSingleFloor(Line3D sectionLine, FloorConfig floor)
    {
        if (floor.AlignmentPoints.Count == 0)
        {
            return new FloorAlignmentResult
            {
                FloorName = floor.Name,
                IsBaseFloor = true,
                CanParticipate = true,
                SectionLine = sectionLine
            };
        }

        if (!FloorSectionLineTransformer.TryValidateAlignmentPoints(floor.AlignmentPoints, out var error))
        {
            return new FloorAlignmentResult
            {
                FloorName = floor.Name,
                IsBaseFloor = true,
                CanParticipate = true,
                SectionLine = sectionLine,
                Issue = new FloorAlignmentIssue
                {
                    Kind = FloorAlignmentIssueKind.AlignmentPointsInvalid,
                    Message = $"单楼层对齐点无效，已回退到原始剖切线: {error}"
                }
            };
        }

        return new FloorAlignmentResult
        {
            FloorName = floor.Name,
            IsBaseFloor = true,
            CanParticipate = true,
            SectionLine = sectionLine
        };
    }
}
