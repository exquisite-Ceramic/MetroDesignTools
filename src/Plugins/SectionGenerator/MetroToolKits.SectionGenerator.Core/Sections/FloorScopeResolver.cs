using MetroToolKits.Foundation.Core.Geometry;

namespace MetroToolKits.SectionGenerator.Core.Sections;

/// <summary>
/// 楼层范围问题类型。
/// </summary>
public enum FloorScopeIssueKind
{
    None = 0,
    TargetFloorInvalid = 1,
    FloorScopeMissing = 2,
    FloorScopeInvalid = 3,
    LocalScopeInvalid = 4
}

/// <summary>
/// 楼层范围问题。
/// </summary>
public sealed record class FloorScopeIssue
{
    public FloorScopeIssueKind Kind { get; init; }
    public string Message { get; init; } = string.Empty;
}

/// <summary>
/// 单楼层执行上下文。
/// </summary>
public sealed class FloorExecutionContext
{
    public string FloorName { get; init; } = string.Empty;
    public bool IsBaseFloor { get; init; }
    public bool CanParticipate { get; init; }
    public Line3D? SectionLine { get; init; }
    public ScopeBounds2D? EffectiveScope { get; init; }
    public FloorAlignmentIssue? AlignmentIssue { get; init; }
    public FloorScopeIssue? ScopeIssue { get; init; }
}

/// <summary>
/// 楼层范围解析结果。
/// </summary>
public sealed class FloorScopeResolution
{
    public IReadOnlyDictionary<string, FloorExecutionContext> Floors { get; init; } =
        new Dictionary<string, FloorExecutionContext>(StringComparer.OrdinalIgnoreCase);

    public FloorScopeIssue? FatalIssue { get; init; }
}

/// <summary>
/// 将“整层范围 + 局部范围”解析为每层实际参与的有效作用域。
/// </summary>
public sealed class FloorScopeResolver
{
    public FloorScopeResolution Resolve(
        SectionConfig config,
        FloorAlignmentResolution alignmentResolution,
        string? targetFloorName,
        ScopeBounds2D? localScopeBounds,
        string? localScopeFloorName)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(alignmentResolution);

        var floors = config.Floors ?? new List<FloorConfig>();
        var results = new Dictionary<string, FloorExecutionContext>(StringComparer.OrdinalIgnoreCase);
        if (floors.Count == 0)
            return new FloorScopeResolution { Floors = results };

        var targetedFloors = ResolveTargetFloors(floors, targetFloorName, out var targetIssue);
        if (targetIssue != null)
            return new FloorScopeResolution { Floors = results, FatalIssue = targetIssue };

        var targetFloorList = targetedFloors.ToList();
        var isSingleFloorMode = targetFloorList.Count <= 1;

        FloorConfig? localScopeSourceFloor = null;
        if (localScopeBounds.HasValue)
        {
            localScopeSourceFloor = targetFloorList.FirstOrDefault(f =>
                string.Equals(f.Name, localScopeFloorName, StringComparison.OrdinalIgnoreCase));

            if (localScopeSourceFloor == null)
            {
                return new FloorScopeResolution
                {
                    Floors = results,
                    FatalIssue = new FloorScopeIssue
                    {
                        Kind = FloorScopeIssueKind.LocalScopeInvalid,
                        Message = $"局部范围定义楼层 {localScopeFloorName ?? "<empty>"} 不存在。"
                    }
                };
            }

            if (!localScopeBounds.Value.IsValid())
            {
                return new FloorScopeResolution
                {
                    Floors = results,
                    FatalIssue = new FloorScopeIssue
                    {
                        Kind = FloorScopeIssueKind.LocalScopeInvalid,
                        Message = "局部范围框无效。"
                    }
                };
            }
        }

        foreach (var floor in targetFloorList)
        {
            if (!alignmentResolution.Floors.TryGetValue(floor.Name, out var alignment))
            {
                results[floor.Name] = new FloorExecutionContext
                {
                    FloorName = floor.Name,
                    IsBaseFloor = string.Equals(floor.Name, config.AlignmentBaseFloorName, StringComparison.OrdinalIgnoreCase),
                    CanParticipate = false,
                    ScopeIssue = new FloorScopeIssue
                    {
                        Kind = FloorScopeIssueKind.TargetFloorInvalid,
                        Message = $"未解析到楼层 {floor.Name} 的剖切线。"
                    }
                };
                continue;
            }

            var isBaseFloor = alignment.IsBaseFloor;
            var persistentScope = floor.ScopeBounds;
            if (isSingleFloorMode && !persistentScope.HasValue)
            {
                results[floor.Name] = new FloorExecutionContext
                {
                    FloorName = alignment.FloorName,
                    IsBaseFloor = alignment.IsBaseFloor,
                    CanParticipate = alignment.CanParticipate && alignment.SectionLine.HasValue,
                    SectionLine = alignment.SectionLine,
                    EffectiveScope = localScopeBounds,
                    AlignmentIssue = alignment.Issue
                };
                continue;
            }

            if (!persistentScope.HasValue)
            {
                var issue = new FloorScopeIssue
                {
                    Kind = FloorScopeIssueKind.FloorScopeMissing,
                    Message = $"楼层 {floor.Name} 缺少整层范围框。"
                };

                if (isBaseFloor && !isSingleFloorMode)
                    return new FloorScopeResolution { Floors = results, FatalIssue = issue };

                results[floor.Name] = BuildSkippedContext(alignment, issue);
                continue;
            }

            if (!persistentScope.Value.IsValid())
            {
                if (isSingleFloorMode)
                {
                    results[floor.Name] = new FloorExecutionContext
                    {
                        FloorName = alignment.FloorName,
                        IsBaseFloor = alignment.IsBaseFloor,
                        CanParticipate = alignment.CanParticipate && alignment.SectionLine.HasValue,
                        SectionLine = alignment.SectionLine,
                        EffectiveScope = localScopeBounds,
                        AlignmentIssue = alignment.Issue,
                        ScopeIssue = new FloorScopeIssue
                        {
                            Kind = FloorScopeIssueKind.FloorScopeInvalid,
                            Message = $"楼层 {floor.Name} 的整层范围框无效，已回退到当前局部范围/无范围模式。"
                        }
                    };
                    continue;
                }

                var issue = new FloorScopeIssue
                {
                    Kind = FloorScopeIssueKind.FloorScopeInvalid,
                    Message = $"楼层 {floor.Name} 的整层范围框无效。"
                };

                if (isBaseFloor && !isSingleFloorMode)
                    return new FloorScopeResolution { Floors = results, FatalIssue = issue };

                results[floor.Name] = BuildSkippedContext(alignment, issue);
                continue;
            }

            var effectiveScope = persistentScope.Value;
            if (localScopeBounds.HasValue)
            {
                var mappedLocalScope = MapLocalScope(
                    localScopeSourceFloor!,
                    floor,
                    localScopeBounds.Value,
                    alignmentResolution);

                if (!mappedLocalScope.HasValue || !mappedLocalScope.Value.IsValid())
                {
                    results[floor.Name] = BuildSkippedContext(
                        alignment,
                        new FloorScopeIssue
                        {
                            Kind = FloorScopeIssueKind.LocalScopeInvalid,
                            Message = $"局部范围无法映射到楼层 {floor.Name}。"
                        });
                    continue;
                }

                var intersection = effectiveScope.Intersect(mappedLocalScope.Value);
                if (!intersection.HasValue)
                {
                    results[floor.Name] = BuildSkippedContext(
                        alignment,
                        new FloorScopeIssue
                        {
                            Kind = FloorScopeIssueKind.LocalScopeInvalid,
                            Message = $"局部范围与楼层 {floor.Name} 的整层范围没有交集。"
                        });
                    continue;
                }

                effectiveScope = intersection.Value;
            }

            results[floor.Name] = new FloorExecutionContext
            {
                FloorName = alignment.FloorName,
                IsBaseFloor = alignment.IsBaseFloor,
                CanParticipate = alignment.CanParticipate && alignment.SectionLine.HasValue,
                SectionLine = alignment.SectionLine,
                EffectiveScope = effectiveScope,
                AlignmentIssue = alignment.Issue
            };
        }

        return new FloorScopeResolution { Floors = results };
    }

    private static IEnumerable<FloorConfig> ResolveTargetFloors(
        IReadOnlyList<FloorConfig> floors,
        string? targetFloorName,
        out FloorScopeIssue? issue)
    {
        issue = null;

        if (string.IsNullOrWhiteSpace(targetFloorName))
            return floors;

        var target = floors.FirstOrDefault(f =>
            string.Equals(f.Name, targetFloorName, StringComparison.OrdinalIgnoreCase));

        if (target == null)
        {
            issue = new FloorScopeIssue
            {
                Kind = FloorScopeIssueKind.TargetFloorInvalid,
                Message = $"未找到目标楼层 {targetFloorName}。"
            };

            return Array.Empty<FloorConfig>();
        }

        return new[] { target };
    }

    private static FloorExecutionContext BuildSkippedContext(
        FloorAlignmentResult alignment,
        FloorScopeIssue issue)
    {
        return new FloorExecutionContext
        {
            FloorName = alignment.FloorName,
            IsBaseFloor = alignment.IsBaseFloor,
            CanParticipate = false,
            SectionLine = alignment.SectionLine,
            AlignmentIssue = alignment.Issue,
            ScopeIssue = issue
        };
    }

    private static ScopeBounds2D? MapLocalScope(
        FloorConfig sourceFloor,
        FloorConfig targetFloor,
        ScopeBounds2D sourceScope,
        FloorAlignmentResolution alignmentResolution)
    {
        if (string.Equals(sourceFloor.Name, targetFloor.Name, StringComparison.OrdinalIgnoreCase))
            return sourceScope;

        var sourceAlignment = alignmentResolution.Floors[sourceFloor.Name];
        var targetAlignment = alignmentResolution.Floors[targetFloor.Name];
        if (!sourceAlignment.CanParticipate || !targetAlignment.CanParticipate)
            return null;

        if (!FloorSectionLineTransformer.TryValidateAlignmentPoints(sourceFloor.AlignmentPoints, out _) ||
            !FloorSectionLineTransformer.TryValidateAlignmentPoints(targetFloor.AlignmentPoints, out _))
        {
            return null;
        }

        var transform = FloorSectionLineTransformer.CreateAlignmentTransform(
            sourceFloor.AlignmentPoints,
            targetFloor.AlignmentPoints);

        var transformedCorners = sourceScope
            .GetCornerPoints()
            .Select(transform.Transform)
            .ToList();

        var mapped = ScopeBounds2D.FromPoints(transformedCorners);
        return mapped.IsValid() ? mapped : null;
    }
}
