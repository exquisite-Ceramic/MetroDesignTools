using System.Collections.Generic;
using System.Linq;
using SectionGenerator.Core.Geometry;

namespace SectionGenerator.Core.Sections;

/// <summary>
/// 剖面生成器实现 - 领域层
/// 使用构建器模式组装各层
/// </summary>
public sealed class SectionGenerator : ISectionGenerator
{
    private readonly IEnumerable<ILayerBuilder> _layerBuilders;

    /// <summary>
    /// 创建剖面生成器
    /// </summary>
    /// <param name="layerBuilders">层构建器集合，定义了层的组合和顺序</param>
    public SectionGenerator(IEnumerable<ILayerBuilder> layerBuilders)
    {
        _layerBuilders = layerBuilders;
    }

    /// <summary>
    /// 生成剖面
    /// </summary>
    public SectionResult Generate(SectionDefinition def, SectionSettings settings)
    {
        // 1. 构建各层
        var layers = BuildLayers(settings, def.SampleStep);
        
        // 2. 生成剖面多段线
        var polyline = GenerateSectionPolyline(layers, def.SampleStep);
        
        return new SectionResult(polyline, layers);
    }

    /// <summary>
    /// 构建各层 - 使用注入的构建器按顺序组装
    /// </summary>
    private IReadOnlyList<LayerPosition> BuildLayers(SectionSettings settings, double length)
    {
        var context = new SectionBuildContext(settings, length);
        var layers = new List<LayerPosition>();

        foreach (var builder in _layerBuilders)
        {
            var layer = builder.Build(context);
            layers.Add(layer);
            context.AddLayer(layer);
        }

        return layers.AsReadOnly();
    }

    /// <summary>
    /// 生成剖面多段线
    /// </summary>
    private Polyline2 GenerateSectionPolyline(IReadOnlyList<LayerPosition> layers, double length)
    {
        var vertices = new List<Vec2>();
        
        // 找出所有层的最小和最大Y坐标
        double maxY = layers.Max(l => System.Math.Max(l.TopStart, l.TopEnd));
        double minY = layers.Min(l => System.Math.Min(l.BottomStart, l.BottomEnd));
        
        // 生成矩形轮廓的四个顶点
        vertices.Add(new Vec2(0, minY));      // 左下
        vertices.Add(new Vec2(length, minY)); // 右下
        vertices.Add(new Vec2(length, maxY)); // 右上
        vertices.Add(new Vec2(0, maxY));      // 左上

        return new Polyline2(vertices, true);
    }
}

/// <summary>
/// 层位置定义
/// </summary>
public sealed record LayerPosition(
    string Name,
    double BottomStart,
    double BottomEnd,
    double TopStart,
    double TopEnd,
    string HatchPattern
);

/// <summary>
/// 剖面生成上下文
/// </summary>
public sealed record SectionGenerationContext(
    SectionDefinition Definition,
    SectionSettings Settings,
    IReadOnlyList<double> IntersectionParams
);
