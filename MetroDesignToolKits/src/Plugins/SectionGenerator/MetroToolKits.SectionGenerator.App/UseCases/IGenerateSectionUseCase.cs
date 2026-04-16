using MetroToolKits.Foundation.Core.Geometry;
using MetroToolKits.SectionGenerator.Core.Sections;

namespace MetroToolKits.SectionGenerator.App.UseCases;

/// <summary>
/// 剖面生成用例接口
/// </summary>
public interface IGenerateSectionUseCase
{
    /// <summary>
    /// 执行剖面生成
    /// </summary>
    /// <param name="request">生成请求</param>
    /// <returns>生成结果（含剖面块名称）</returns>
    GenerateSectionResult Execute(GenerateSectionRequest request);
}

/// <summary>
/// 剖面生成请求
/// </summary>
public sealed class GenerateSectionRequest
{
    /// <summary>剖切线起点（平面坐标）</summary>
    public Point3D CutLineStart { get; set; }

    /// <summary>剖切线终点（平面坐标）</summary>
    public Point3D CutLineEnd { get; set; }

    /// <summary>视图深度（看线深度，mm）</summary>
    public double ViewDepth { get; set; } = 3000;

    /// <summary>剖面块插入点</summary>
    public Point3D InsertionPoint { get; set; }

    /// <summary>楼层配置（null 则从配置文件读取）</summary>
    public FloorConfig? FloorConfig { get; set; }
}

/// <summary>
/// 剖面生成结果
/// </summary>
public sealed class GenerateSectionResult
{
    public bool Success { get; set; }
    public string? BlockName { get; set; }
    public string? ErrorMessage { get; set; }
    public SectionGeometryData? GeometryData { get; set; }
}
