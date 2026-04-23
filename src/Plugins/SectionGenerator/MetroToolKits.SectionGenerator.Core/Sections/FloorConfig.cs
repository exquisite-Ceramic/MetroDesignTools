using MetroToolKits.Foundation.Building.Types;
using MetroToolKits.Foundation.Core.Geometry;

namespace MetroToolKits.SectionGenerator.Core.Sections;

/// <summary>
/// 楼层配置
/// </summary>
public sealed class FloorConfig
{
    public string Name { get; set; } = string.Empty;
    public double Height { get; set; } = 3000.0;

    // 兼容旧配置字段，运行期改由边界板模板驱动。
    public double FinishThickness { get; set; } = 120.0;
    public double BottomSlabThickness { get; set; } = 800.0;
    public double TopSlabThickness { get; set; } = 600.0;
    public bool HasSlope { get; set; }
    public double SlopeValue { get; set; }
    public string SlopeTarget { get; set; } = "StructuralSlab";

    /// <summary>底边界板配置。</summary>
    public BoundarySlabConfig BottomBoundarySlab { get; set; } = new();

    /// <summary>顶边界板配置。</summary>
    public BoundarySlabConfig TopBoundarySlab { get; set; } = new();

    /// <summary>当前楼层对齐点（原点、X 方向点、Y 方向点）</summary>
    public List<Point3D> AlignmentPoints { get; set; } = new();

    /// <summary>当前楼层整层范围框。</summary>
    public ScopeBounds2D? ScopeBounds { get; set; }
}

/// <summary>
/// 全局剖面配置
/// </summary>
public sealed class SectionConfig
{
    // 兼容旧全局坡度字段。
    public bool GlobalSlopeEnabled { get; set; }
    public double GlobalSlopeValue { get; set; } = 0.002;
    public string GlobalSlopeTarget { get; set; } = "StructuralSlab";

    public bool GlobalTopSlopeEnabled { get; set; }
    public double GlobalTopSlopeValue { get; set; } = 0.002;
    public string GlobalTopSlopeTarget { get; set; } = "StructuralSlab";

    public bool GlobalBottomSlopeEnabled { get; set; }
    public double GlobalBottomSlopeValue { get; set; }
    public string GlobalBottomSlopeTarget { get; set; } = "StructuralSlab";

    public string AlignmentBaseFloorName { get; set; } = string.Empty;
    public List<FloorConfig> Floors { get; set; } = new();
}
