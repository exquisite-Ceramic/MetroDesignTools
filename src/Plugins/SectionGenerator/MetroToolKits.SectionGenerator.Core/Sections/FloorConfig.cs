using System.Text.Json.Serialization;
using MetroToolKits.Foundation.Core.Diagnostics;
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

    /// <summary>当前楼层对齐点（原点、X 方向点、Y 方向点）</summary>
    public List<Point3D> AlignmentPoints { get; set; } = new();
}

/// <summary>
/// 全局剖面配置
/// </summary>
public sealed class SectionConfig
{
    public bool GlobalSlopeEnabled { get; set; }
    public double GlobalSlopeValue { get; set; } = 0.002;
    public string GlobalSlopeTarget { get; set; } = "StructuralSlab";
    public string AlignmentBaseFloorName { get; set; } = string.Empty;
    public List<FloorConfig> Floors { get; set; } = new();

    /// <summary>读取仓储时附带的运行期诊断，不参与持久化。</summary>
    [JsonIgnore]
    public List<OperationDiagnostic> RuntimeDiagnostics { get; set; } = new();
}
