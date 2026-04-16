using System.Collections.Generic;
using System.Linq;

namespace SectionGenerator.Core.Sections;

/// <summary>
/// 看线层构建器
/// 考虑遮挡情况，将看线分为可见段和不可见段
/// </summary>
public sealed class SightLineLayerBuilder : ILayerBuilder
{
    public string LayerName => "看线";

    /// <summary>
    /// 看线高度（距地面）
    /// </summary>
    public double Height { get; set; } = 1100.0;

    /// <summary>
    /// 人眼高度（用于计算视线）
    /// </summary>
    public double EyeHeight { get; set; } = 1600.0;

    /// <summary>
    /// 看线计算器
    /// </summary>
    public ISightLineCalculator? Calculator { get; set; }

    public LayerPosition Build(SectionBuildContext context)
    {
        // 看线是一个虚拟层，实际绘制时需要根据可见性分段
        // 这里返回一个基础位置，具体的可见段计算在后续处理
        var slopeDelta = context.CalculateSlopeDelta();
        var heightAtStart = Height;
        var heightAtEnd = Height + slopeDelta * (Height / context.Settings.FloorHeight);

        return new LayerPosition(
            LayerName,
            heightAtStart,
            heightAtEnd,
            heightAtStart,
            heightAtEnd,
            "看线"
        );
    }

    /// <summary>
    /// 计算看线可见段（需要在层构建完成后调用）
    /// </summary>
    public SightLineResult CalculateSightLine(SectionBuildContext context)
    {
        var calculator = Calculator ?? new SightLineCalculator();

        // 从已构建的层中提取遮挡物
        var obstacles = ExtractObstacles(context);

        // 计算可见段
        var visibleSegments = calculator.CalculateVisibleSegments(
            EyeHeight,
            Height,
            obstacles,
            context.Length
        );

        // 找出实际造成遮挡的物体
        var blockingObstacles = FindBlockingObstacles(obstacles, visibleSegments, context.Length);

        return new SightLineResult(Height, visibleSegments, blockingObstacles);
    }

    /// <summary>
    /// 从已构建的层中提取遮挡物
    /// </summary>
    private IReadOnlyList<Obstacle> ExtractObstacles(SectionBuildContext context)
    {
        var obstacles = new List<Obstacle>();

        foreach (var layer in context.BuiltLayers)
        {
            // 只有实体层才可能是遮挡物（底板、顶板、墙等）
            // 看线本身不是遮挡物
            if (IsSolidLayer(layer.Name))
            {
                obstacles.Add(new Obstacle(
                    0,                      // 起点X
                    context.Length,         // 终点X
                    layer.BottomStart,      // 底部Y
                    layer.TopStart          // 顶部Y
                ));
            }
        }

        return obstacles;
    }

    /// <summary>
    /// 判断是否为实体层（可能遮挡视线）
    /// </summary>
    private bool IsSolidLayer(string layerName)
    {
        // 实体层列表
        var solidLayers = new[] { "底板", "顶板", "墙", "梁", "柱", "填充墙" };
        return solidLayers.Contains(layerName);
    }

    /// <summary>
    /// 找出实际造成遮挡的障碍物
    /// </summary>
    private IReadOnlyList<Obstacle> FindBlockingObstacles(
        IReadOnlyList<Obstacle> allObstacles,
        IReadOnlyList<VisibleSegment> visibleSegments,
        double sectionLength)
    {
        // 如果整个剖面都可见，则没有遮挡物
        if (visibleSegments.Count == 1 &&
            visibleSegments[0].StartX <= 0 &&
            visibleSegments[0].EndX >= sectionLength)
        {
            return new List<Obstacle>();
        }

        // 有遮挡，返回所有可能的遮挡物
        // 实际应用中可能需要更精确的判断
        return allObstacles;
    }
}

/// <summary>
/// 剖切构件构建器 - 用于添加墙体等遮挡物
/// </summary>
public sealed class WallLayerBuilder : ILayerBuilder
{
    public string LayerName => "墙";

    /// <summary>
    /// 墙体位置（距剖面起点的距离）
    /// </summary>
    public double Position { get; set; } = 0;

    /// <summary>
    /// 墙体厚度
    /// </summary>
    public double Thickness { get; set; } = 200.0;

    /// <summary>
    /// 墙体高度
    /// </summary>
    public double Height { get; set; } = 3000.0;

    /// <summary>
    /// 墙体底部标高
    /// </summary>
    public double BottomElevation { get; set; } = 0;

    public LayerPosition Build(SectionBuildContext context)
    {
        return new LayerPosition(
            LayerName,
            BottomElevation,
            BottomElevation,
            BottomElevation + Height,
            BottomElevation + Height,
            "SOLID"  // 实体填充
        );
    }
}
