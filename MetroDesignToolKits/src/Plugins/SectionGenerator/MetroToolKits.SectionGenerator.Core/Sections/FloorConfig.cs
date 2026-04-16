using MetroToolKits.Foundation.Core.Geometry;

namespace MetroToolKits.SectionGenerator.Core.Sections;

/// <summary>
/// 楼层配置
/// </summary>
public sealed class FloorConfig
{
    public string Name { get; set; } = string.Empty;
    public double Height { get; set; } = 3000.0;
    public double FinishThickness { get; set; } = 120.0;
    public double BottomSlabThickness { get; set; } = 800.0;
    public double TopSlabThickness { get; set; } = 600.0;
    public bool HasSlope { get; set; }
    public double SlopeValue { get; set; }
    public string SlopeTarget { get; set; } = "StructuralSlab";

    /// <summary>对齐源点（平面坐标系三点）</summary>
    public List<Point3D> AlignmentSourcePoints { get; set; } = new();

    /// <summary>对齐目标点（剖面坐标系三点）</summary>
    public List<Point3D> AlignmentTargetPoints { get; set; } = new();
}

/// <summary>
/// 全局剖面配置
/// </summary>
public sealed class SectionConfig
{
    public bool GlobalSlopeEnabled { get; set; }
    public double GlobalSlopeValue { get; set; } = 0.002;
    public string GlobalSlopeTarget { get; set; } = "StructuralSlab";
    public List<FloorConfig> Floors { get; set; } = new();
}
