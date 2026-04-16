using MetroToolKits.Foundation.Core.Geometry;
using MetroToolKits.SectionGenerator.Core.Sections;

namespace MetroToolKits.SectionGenerator.App.Abstractions;

/// <summary>
/// 绘图服务接口 - 将剖面几何数据绘制为 AutoCAD 块
/// </summary>
public interface IDrawingService
{
    /// <summary>
    /// 将剖面几何数据绘制为块，插入到指定点
    /// </summary>
    /// <param name="geometryData">剖面几何数据</param>
    /// <param name="insertionPoint">块插入点</param>
    /// <param name="floorConfig">楼层配置（用于标注）</param>
    /// <returns>创建的块名称</returns>
    string DrawSectionBlock(SectionGeometryData geometryData, Point3D insertionPoint, FloorConfig floorConfig);
}
