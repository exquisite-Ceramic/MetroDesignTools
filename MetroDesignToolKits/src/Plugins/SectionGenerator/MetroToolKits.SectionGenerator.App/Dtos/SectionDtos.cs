using System.Collections.Generic;
using System.Linq;

namespace SectionGenerator.App.Dtos;

/// <summary>
/// 层数据传输对象
/// </summary>
public sealed record LayerDto(
    string Name,
    double Thickness,
    double BottomElevation,
    string HatchPattern
);

/// <summary>
/// 剖面结果数据传输对象
/// </summary>
public sealed record SectionResultDto(
    double TotalHeight,
    double Length,
    IReadOnlyList<LayerDto> Layers,
    IReadOnlyList<Point2dDto> PolylineVertices
);

/// <summary>
/// 二维点数据传输对象
/// </summary>
public sealed record Point2dDto(double X, double Y);

/// <summary>
/// 剖面数据传输对象映射器
/// </summary>
public static class SectionDtoMapper
{
    public static LayerDto ToDto(this Core.Sections.LayerPosition layer)
    {
        return new LayerDto(
            layer.Name,
            layer.TopStart - layer.BottomStart,
            layer.BottomStart,
            layer.HatchPattern
        );
    }
    
    public static SectionResultDto ToDto(this Core.Sections.SectionResult result)
    {
        return new SectionResultDto(
            result.Layers.Max(l => l.TopStart) - result.Layers.Min(l => l.BottomStart),
            result.SectionPolyline.Vertices.Count > 1 
                ? result.SectionPolyline.Vertices[1].X - result.SectionPolyline.Vertices[0].X 
                : 0,
            result.Layers.Select(l => l.ToDto()).ToList(),
            result.SectionPolyline.Vertices.Select(v => new Point2dDto(v.X, v.Y)).ToList()
        );
    }
}
