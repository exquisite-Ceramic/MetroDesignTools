using System.Collections.Generic;
using SectionGenerator.Core.Geometry;

namespace SectionGenerator.Core.Sections;

/// <summary>
/// 看线计算器接口
/// </summary>
public interface ISightLineCalculator
{
    /// <summary>
    /// 计算看线可见段
    /// </summary>
    /// <param name="eyeHeight">人眼高度</param>
    /// <param name="targetHeight">看线高度</param>
    /// <param name="obstacles">遮挡物列表（剖切构件）</param>
    /// <param name="sectionLength">剖面长度</param>
    /// <returns>可见段列表</returns>
    IReadOnlyList<VisibleSegment> CalculateVisibleSegments(
        double eyeHeight,
        double targetHeight,
        IReadOnlyList<Obstacle> obstacles,
        double sectionLength);
}

/// <summary>
/// 遮挡物定义
/// </summary>
public sealed record Obstacle(
    double StartX,      // 起点X坐标
    double EndX,        // 终点X坐标
    double BottomY,     // 底部Y坐标
    double TopY         // 顶部Y坐标
);

/// <summary>
/// 可见段定义
/// </summary>
public sealed record VisibleSegment(
    double StartX,
    double EndX,
    double Height
);

/// <summary>
/// 看线结果
/// </summary>
public sealed record SightLineResult(
    double Height,
    IReadOnlyList<VisibleSegment> VisibleSegments,
    IReadOnlyList<Obstacle> BlockingObstacles
);
