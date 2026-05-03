using MetroToolKits.Foundation.Core.Diagnostics;
using MetroToolKits.SectionGenerator.Core.Sections;

namespace MetroToolKits.SectionGenerator.App.Models;

/// <summary>
/// 图纸级剖面配置文档。
/// 业务配置、输出配置和运行时来源信息在应用层组合，不再直接回灌到 Core 配置模型。
/// </summary>
public sealed class LoadedSectionConfig
{
    public SectionConfig Config { get; set; } = new();

    public SectionOutputConfig OutputConfig { get; set; } = new();

    public List<OperationDiagnostic> RuntimeDiagnostics { get; set; } = new();

    public SectionConfigRuntimeState RuntimeState { get; set; } = new();
}

public sealed class FloorConfigSaveResult
{
    public bool Success { get; init; }

    public string? ErrorMessage { get; init; }

    public IReadOnlyList<OperationDiagnostic> Diagnostics { get; init; } = Array.Empty<OperationDiagnostic>();
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

public sealed class SectionOutputConfig
{
    public AnnotationOptions AnnotationOptions { get; set; } = new();

    public HatchOptions HatchOptions { get; set; } = new();

    public SightLineOptions SightLineOptions { get; set; } = new();

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

public sealed class SightLineOptions
{
    public bool Enabled { get; set; } = true;

    public bool IncludeSlabs { get; set; }

    public bool IncludeWalls { get; set; } = true;

    public bool IncludeColumns { get; set; } = true;
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
