using System.Text.Json.Serialization;
using MetroToolKits.Foundation.Building.Types;
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
    public SectionOutputConfig OutputConfig { get; set; } = new();

    /// <summary>读取仓储时附带的运行期诊断，不参与持久化。</summary>
    [JsonIgnore]
    public List<OperationDiagnostic> RuntimeDiagnostics { get; set; } = new();

    /// <summary>当前配置的运行期来源信息，不参与持久化。</summary>
    [JsonIgnore]
    public SectionConfigRuntimeState RuntimeState { get; set; } = new();
}

public sealed class SectionOutputConfig
{
    public AnnotationOptions AnnotationOptions { get; set; } = new();
    public HatchOptions HatchOptions { get; set; } = new();
    public LayerOptions LayerOptions { get; set; } = new();
}

public sealed class AnnotationOptions
{
    public bool GenerateAnnotations { get; set; }
}

public sealed class HatchOptions
{
    public bool Enabled { get; set; }
    public HatchStyleOptions WallHatch { get; set; } = HatchStyleOptions.CreateDefault();
    public HatchStyleOptions ColumnHatch { get; set; } = HatchStyleOptions.CreateDefault();
    public HatchStyleOptions SlabHatch { get; set; } = HatchStyleOptions.CreateDefault();
}

public sealed class HatchStyleOptions
{
    public string PatternName { get; set; } = "ANSI31";
    public double Scale { get; set; } = 100.0;
    public double Angle { get; set; }
    public bool UseByLayer { get; set; } = true;

    public static HatchStyleOptions CreateDefault() => new();
}

public sealed class LayerOptions
{
    public string CutLineLayer { get; set; } = "MK_剖切线";
    public string SightLineLayer { get; set; } = "MK_看线";
    public string AnnotationLayer { get; set; } = "MK_标注";
    public string WallHatchLayer { get; set; } = "MK_墙填充";
    public string ColumnHatchLayer { get; set; } = "MK_柱填充";
    public string SlabHatchLayer { get; set; } = "MK_楼板填充";
    public string StructuralLayer { get; set; } = "MK_结构输出";
    public string FinishLayer { get; set; } = "MK_装修输出";
}

public enum SectionConfigStorageSource
{
    EmbeddedDwg,
    TransientUnsavedDrawing,
    Missing
}

public sealed class SectionConfigRuntimeState
{
    public SectionConfigStorageSource Source { get; set; } = SectionConfigStorageSource.Missing;
    public bool IsCurrentDrawingSaved { get; set; }
    public bool HasPersistedConfig { get; set; }
    public string DrawingDisplayName { get; set; } = string.Empty;
    public string? DrawingPath { get; set; }
}
