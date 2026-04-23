namespace MetroToolKits.Foundation.Building.Types;

/// <summary>
/// 墙体附加层所在侧别。
/// </summary>
public enum WallLayerSide
{
    Left,
    Right
}

/// <summary>
/// 核心墙体识别模式。
/// </summary>
public enum WallCoreRecognitionMode
{
    Centerline,
    BoundaryPair
}

/// <summary>
/// 墙体竖向锚定模式。
/// </summary>
public enum WallVerticalAnchorMode
{
    StructuralSlabFaces,
    FinishSurfaceFaces
}

/// <summary>
/// 核心墙体规则。
/// </summary>
public sealed class WallCoreRule
{
    public string Name { get; set; } = "结构芯";
    public double Thickness { get; set; } = 200.0;
    public string MaterialOrCategory { get; set; } = "结构";
    public bool VisibleInSection { get; set; } = true;
    public WallCoreRecognitionMode RecognitionMode { get; set; } = WallCoreRecognitionMode.BoundaryPair;
}

/// <summary>
/// 墙体附加层规则。
/// </summary>
public sealed class WallLayerRule
{
    public string Name { get; set; } = string.Empty;
    public WallLayerSide Side { get; set; }
    public int Order { get; set; }
    public double Thickness { get; set; }
    public string MaterialOrCategory { get; set; } = string.Empty;
    public bool VisibleInSection { get; set; } = true;
}

/// <summary>
/// 墙体构造模板。
/// </summary>
public sealed class WallAssemblyTemplate
{
    public string TemplateId { get; set; } = string.Empty;
    public string TemplateName { get; set; } = string.Empty;
    public WallVerticalAnchorMode VerticalAnchorMode { get; set; } = WallVerticalAnchorMode.StructuralSlabFaces;
    public WallCoreRule CoreRule { get; set; } = new();
    public List<WallLayerRule> LeftLayers { get; set; } = new();
    public List<WallLayerRule> RightLayers { get; set; } = new();
}
