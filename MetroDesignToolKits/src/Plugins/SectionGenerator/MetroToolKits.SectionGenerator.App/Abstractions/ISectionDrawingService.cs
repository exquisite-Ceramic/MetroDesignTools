using System.Collections.Generic;
using SectionGenerator.Core.Geometry;
using SectionGenerator.Core.Sections;

namespace SectionGenerator.App.Abstractions;

/// <summary>
/// 剖面绘制服务接口 - 应用层
/// </summary>
public interface ISectionDrawingService
{
    /// <summary>
    /// 绘制剖面图
    /// </summary>
    void DrawSection(SectionResult result, IReadOnlyList<double> intersectionParams);
}
