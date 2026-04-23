namespace MetroToolKits.Foundation.Building.Types;

/// <summary>
/// 楼板附加层所在侧别。
/// </summary>
public enum SlabLayerSide
{
    Top,
    Bottom
}

/// <summary>
/// 楼板与墙体交接策略。
/// </summary>
public enum SlabWallJunctionMode
{
    StopAtWallFace,
    ContinueUnderWall,
    ReturnUpAtWallFace
}

/// <summary>
/// 核心楼板规则。
/// </summary>
public sealed class SlabCoreRule
{
    public string Name { get; set; } = "结构板";

    public double Thickness { get; set; } = 120.0;

    public string MaterialOrCategory { get; set; } = "结构";

    public bool VisibleInSection { get; set; } = true;
}

/// <summary>
/// 楼板附加层规则。
/// </summary>
public sealed class SlabLayerRule
{
    public string Name { get; set; } = string.Empty;

    public SlabLayerSide Side { get; set; }

    public int Order { get; set; }

    public double Thickness { get; set; }

    public string MaterialOrCategory { get; set; } = string.Empty;

    public bool VisibleInSection { get; set; } = true;
}

/// <summary>
/// 楼板装配模板。
/// </summary>
public sealed class SlabAssemblyTemplate
{
    public string TemplateId { get; set; } = string.Empty;

    public string TemplateName { get; set; } = string.Empty;

    public SlabCoreRule CoreRule { get; set; } = new();

    public List<SlabLayerRule> TopLayers { get; set; } = new();

    public List<SlabLayerRule> BottomLayers { get; set; } = new();

    public SlabWallJunctionMode WallJunctionMode { get; set; } = SlabWallJunctionMode.StopAtWallFace;
}

/// <summary>
/// 楼层边界板配置。
/// </summary>
public sealed class BoundarySlabConfig
{
    public string TemplateId { get; set; } = string.Empty;

    public bool SlopeEnabled { get; set; }

    public double SlopeValue { get; set; }

    public string SlopeTarget { get; set; } = "StructuralSlab";
}
