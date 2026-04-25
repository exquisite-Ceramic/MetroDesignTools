namespace MetroToolKits.SectionGenerator.Contracts.Templates;

public sealed class SlabTemplateCatalogDto
{
    public List<SlabAssemblyTemplateDto> Templates { get; set; } = [];
}

public sealed class SlabAssemblyTemplateDto
{
    public string TemplateId { get; set; } = string.Empty;

    public string TemplateName { get; set; } = string.Empty;

    public SlabCoreRuleDto CoreRule { get; set; } = new();

    public List<SlabLayerRuleDto> TopLayers { get; set; } = [];

    public List<SlabLayerRuleDto> BottomLayers { get; set; } = [];

    public string WallJunctionMode { get; set; } = "StopAtWallFace";
}

public sealed class SlabCoreRuleDto
{
    public string Name { get; set; } = "结构板";

    public double Thickness { get; set; } = 120.0;

    public string MaterialOrCategory { get; set; } = "结构";

    public bool VisibleInSection { get; set; } = true;
}

public sealed class SlabLayerRuleDto
{
    public string Name { get; set; } = string.Empty;

    public string Side { get; set; } = "Top";

    public int Order { get; set; }

    public double Thickness { get; set; }

    public string MaterialOrCategory { get; set; } = string.Empty;

    public bool VisibleInSection { get; set; } = true;
}
