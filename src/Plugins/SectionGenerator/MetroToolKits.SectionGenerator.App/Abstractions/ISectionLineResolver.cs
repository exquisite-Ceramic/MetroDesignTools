using MetroToolKits.Foundation.Core.Geometry;

namespace MetroToolKits.SectionGenerator.App.Abstractions;

/// <summary>
/// 源剖切线解析接口。
/// </summary>
public interface ISectionLineResolver
{
    /// <summary>
    /// 根据源剖切线句柄解析当前线几何；无法解析时返回 null。
    /// </summary>
    Line3D? ResolveCurrentLine(string cutLineHandle);
}
