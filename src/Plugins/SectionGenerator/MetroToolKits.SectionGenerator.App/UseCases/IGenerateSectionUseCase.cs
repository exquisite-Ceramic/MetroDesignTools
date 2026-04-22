using MetroToolKits.Foundation.Core.Geometry;
using MetroToolKits.Foundation.Core.Diagnostics;
using MetroToolKits.SectionGenerator.Core.Sections;

namespace MetroToolKits.SectionGenerator.App.UseCases;

/// <summary>
/// 剖面生成用例接口
/// </summary>
public interface IGenerateSectionUseCase
{
    GenerateSectionResult Execute(GenerateSectionRequest request);
}

/// <summary>
/// 剖面生成请求
/// </summary>
public sealed class GenerateSectionRequest
{
    public string  CutLineHandle      { get; set; } = string.Empty;
    public Point3D CutLineStart       { get; set; }
    public Point3D CutLineEnd         { get; set; }
    public double  ViewDepth          { get; set; } = 3000;
    public Point3D InsertionPoint     { get; set; }
    public double? GeometryAnchorX    { get; set; }
    public string? TargetFloorName    { get; set; }
    public ScopeBounds2D? LocalScopeBounds { get; set; }
    public string? LocalScopeFloorName { get; set; }
    public IReadOnlyList<string> IncludedFloorNames { get; set; } = Array.Empty<string>();
    public bool RequireCompleteIncludedFloors { get; set; }

    /// <summary>指定单层配置（null 则从仓储加载所有楼层）</summary>
    public FloorConfig? FloorConfig { get; set; }
}

/// <summary>
/// 剖面生成结果
/// </summary>
public sealed class GenerateSectionResult
    : OperationResult
{
    public bool Success => Status != OperationStatus.Failed;
    public string? BlockName    { get; set; }
    public string? BlockHandle  { get; set; }
    public int     FloorCount   { get; set; }
    public double  TotalHeight  { get; set; }
    public IReadOnlyList<string> GeneratedFloorNames { get; set; } = Array.Empty<string>();
    public string? ErrorMessage => Failure?.UserMessage;

    /// <summary>兼容旧代码（单层）</summary>
    public SectionGeometryData? GeometryData { get; set; }
}
