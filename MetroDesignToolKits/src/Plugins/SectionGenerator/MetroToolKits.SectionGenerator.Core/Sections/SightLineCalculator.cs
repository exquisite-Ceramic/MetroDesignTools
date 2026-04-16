using System;
using System.Collections.Generic;
using System.Linq;

namespace SectionGenerator.Core.Sections;

/// <summary>
/// 看线计算器实现
/// 使用射线法判断遮挡
/// </summary>
public sealed class SightLineCalculator : ISightLineCalculator
{
    /// <summary>
    /// 计算看线可见段
    /// 算法：从人眼位置向看线位置发射射线，检查与遮挡物的交点
    /// </summary>
    public IReadOnlyList<VisibleSegment> CalculateVisibleSegments(
        double eyeHeight,
        double targetHeight,
        IReadOnlyList<Obstacle> obstacles,
        double sectionLength)
    {
        // 如果没有遮挡物，整个看线都可见
        if (obstacles == null || obstacles.Count == 0)
        {
            return new List<VisibleSegment> 
            { 
                new VisibleSegment(0, sectionLength, targetHeight) 
            };
        }

        // 收集所有遮挡区间的起点和终点
        var blockingIntervals = new List<(double X, bool IsStart, Obstacle Obstacle)>();
        
        foreach (var obstacle in obstacles)
        {
            // 检查这个遮挡物是否真的遮挡看线
            if (IsBlockingSightLine(eyeHeight, targetHeight, obstacle))
            {
                // 计算遮挡区间
                var (startX, endX) = CalculateBlockingInterval(
                    eyeHeight, targetHeight, obstacle, sectionLength);
                
                if (startX < endX)  // 有效区间
                {
                    blockingIntervals.Add((startX, true, obstacle));
                    blockingIntervals.Add((endX, false, obstacle));
                }
            }
        }

        // 按X坐标排序
        blockingIntervals = blockingIntervals.OrderBy(x => x.X).ToList();

        // 合并重叠区间，计算可见段
        return CalculateVisibleSegmentsFromBlockingIntervals(
            blockingIntervals, sectionLength, targetHeight);
    }

    /// <summary>
    /// 判断遮挡物是否遮挡看线
    /// </summary>
    private bool IsBlockingSightLine(double eyeHeight, double targetHeight, Obstacle obstacle)
    {
        // 遮挡物在看线和人眼之间
        double sightLineMinY = Math.Min(eyeHeight, targetHeight);
        double sightLineMaxY = Math.Max(eyeHeight, targetHeight);
        
        // 遮挡物顶部必须高于视线，底部必须低于视线
        return obstacle.TopY > sightLineMinY && obstacle.BottomY < sightLineMaxY;
    }

    /// <summary>
    /// 计算遮挡区间
    /// 从人眼位置向遮挡物边缘发射射线，计算在看线上的交点
    /// </summary>
    private (double StartX, double EndX) CalculateBlockingInterval(
        double eyeHeight,
        double targetHeight,
        Obstacle obstacle,
        double sectionLength)
    {
        // 假设人眼在剖面起点 (0, eyeHeight)
        double eyeX = 0;
        
        // 计算视线与遮挡物左侧的交点在看线上的投影
        double startX = CalculateSightLineIntersection(
            eyeX, eyeHeight, 
            obstacle.StartX, obstacle.TopY,  // 使用顶部，因为视线从上方过来
            targetHeight,
            sectionLength);
        
        // 计算视线与遮挡物右侧的交点在看线上的投影
        double endX = CalculateSightLineIntersection(
            eyeX, eyeHeight,
            obstacle.EndX, obstacle.TopY,
            targetHeight,
            sectionLength);

        // 限制在剖面范围内
        startX = Math.Max(0, startX);
        endX = Math.Min(sectionLength, endX);

        return (startX, endX);
    }

    /// <summary>
    /// 计算视线与看线的交点X坐标
    /// 视线：从 (eyeX, eyeHeight) 到 (targetX, targetY)
    /// 看线：y = targetHeight
    /// </summary>
    private double CalculateSightLineIntersection(
        double eyeX, double eyeHeight,
        double obstacleX, double obstacleY,
        double targetHeight,
        double sectionLength)
    {
        // 视线方向向量
        double dx = obstacleX - eyeX;
        double dy = obstacleY - eyeHeight;
        
        // 如果视线水平，直接返回
        if (Math.Abs(dy) < 1e-6)
        {
            return obstacleX;
        }
        
        // 计算视线延伸到看线高度的X坐标
        // 参数 t = (targetHeight - eyeHeight) / dy
        double t = (targetHeight - eyeHeight) / dy;
        
        // 在看线上的交点
        double intersectionX = eyeX + t * dx;
        
        return intersectionX;
    }

    /// <summary>
    /// 从遮挡区间计算可见段
    /// </summary>
    private IReadOnlyList<VisibleSegment> CalculateVisibleSegmentsFromBlockingIntervals(
        List<(double X, bool IsStart, Obstacle Obstacle)> blockingIntervals,
        double sectionLength,
        double targetHeight)
    {
        var visibleSegments = new List<VisibleSegment>();
        
        if (blockingIntervals.Count == 0)
        {
            // 没有遮挡，全部可见
            visibleSegments.Add(new VisibleSegment(0, sectionLength, targetHeight));
            return visibleSegments;
        }

        // 扫描线算法
        double currentX = 0;
        int blockingCount = 0;
        double lastX = 0;

        foreach (var (x, isStart, obstacle) in blockingIntervals)
        {
            if (blockingCount == 0 && x > currentX)
            {
                // 从 currentX 到 x 是可见的
                visibleSegments.Add(new VisibleSegment(currentX, x, targetHeight));
            }

            if (isStart)
            {
                blockingCount++;
            }
            else
            {
                blockingCount--;
            }

            currentX = x;
            lastX = x;
        }

        // 检查最后一段
        if (currentX < sectionLength && blockingCount == 0)
        {
            visibleSegments.Add(new VisibleSegment(currentX, sectionLength, targetHeight));
        }

        return visibleSegments;
    }
}
