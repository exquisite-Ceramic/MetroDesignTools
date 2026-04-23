using MetroToolKits.Foundation.Core.Geometry;
using MetroToolKits.Foundation.Building.Types;

namespace MetroToolKits.SectionGenerator.Core.Sections;

/// <summary>
/// 单楼层几何快照
/// </summary>
public sealed class FloorSnapshot
{
    public string FloorName     { get; set; } = string.Empty;
    public string GeometryHash  { get; set; } = string.Empty;
    public int    ElementCount  { get; set; }
    public List<string> SourceElementHandles { get; set; } = new();
}

/// <summary>
/// 剖面块快照（存储在块的 XData 中）
/// </summary>
public sealed class SectionSnapshot
{
    public string SectionId              { get; set; } = Guid.NewGuid().ToString();
    public string BlockName              { get; set; } = string.Empty;
    public string SourceCutLineHandle    { get; set; } = string.Empty;
    public Point3D CutLineStart          { get; set; }
    public Point3D CutLineEnd            { get; set; }
    public Point3D InsertionPoint        { get; set; }
    public double? GeometryAnchorX       { get; set; }
    public Point3D? SectionDirection     { get; set; }
    public WallVerticalAnchorMode VerticalAnchorMode { get; set; } = WallVerticalAnchorMode.StructuralSlabFaces;
    public double  ViewDepth             { get; set; } = 3000;
    public string? TargetFloorName       { get; set; }
    public ScopeBounds2D? LocalScopeBounds { get; set; }
    public string? LocalScopeFloorName   { get; set; }
    public DateTime GeneratedAt          { get; set; } = DateTime.Now;
    public double TotalHeight            { get; set; }
    public List<string> GeneratedFloorNames { get; set; } = new();
    public List<FloorSnapshot> FloorSnapshots { get; set; } = new();
}

/// <summary>
/// 剖面更新状态
/// </summary>
public enum SectionUpdateStatus
{
    UpToDate,   // 最新
    Outdated,   // 需要更新
    Unknown     // 无法确定（快照丢失）
}

/// <summary>
/// 剖面检测结果
/// </summary>
public sealed class SectionCheckResult
{
    public string BlockName          { get; set; } = string.Empty;
    public string BlockHandle        { get; set; } = string.Empty;
    public SectionUpdateStatus Status { get; set; }
    public List<string> OutdatedFloors { get; set; } = new();
    public List<string> SkippedFloors { get; set; } = new();
    public string? WarningMessage { get; set; }
    public SectionSnapshot? Snapshot { get; set; }
}
