namespace MetroToolKits.SectionGenerator.Contracts.Templates;

public sealed class WallTemplateCatalogDto
{
    public List<WallAssemblyTemplateDto> Templates { get; set; } = [];
}

public sealed class WallAssemblyTemplateDto
{
    public string TemplateId { get; set; } = string.Empty;

    public string TemplateName { get; set; } = string.Empty;

    public string VerticalAnchorMode { get; set; } = "StructuralSlabFaces";

    public WallCoreRuleDto CoreRule { get; set; } = new();

    public List<WallLayerRuleDto> LeftLayers { get; set; } = [];

    public List<WallLayerRuleDto> RightLayers { get; set; } = [];
}

public sealed class WallCoreRuleDto
{
    public string Name { get; set; } = "结构芯";

    public double Thickness { get; set; } = 200.0;

    public string MaterialOrCategory { get; set; } = "结构";

    public bool VisibleInSection { get; set; } = true;

    public string RecognitionMode { get; set; } = "BoundaryPair";
}

public sealed class WallLayerRuleDto
{
    public string Name { get; set; } = string.Empty;

    public string Side { get; set; } = "Left";

    public int Order { get; set; }

    public double Thickness { get; set; }

    public string MaterialOrCategory { get; set; } = string.Empty;

    public bool VisibleInSection { get; set; } = true;
}
