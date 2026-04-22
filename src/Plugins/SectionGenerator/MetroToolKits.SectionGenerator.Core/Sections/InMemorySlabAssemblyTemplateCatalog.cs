using MetroToolKits.Foundation.Building.Types;

namespace MetroToolKits.SectionGenerator.Core.Sections;

/// <summary>
/// 用于测试和默认回退的内存楼板模板目录。
/// </summary>
public sealed class InMemorySlabAssemblyTemplateCatalog : ISlabAssemblyTemplateCatalog
{
    private readonly List<SlabAssemblyTemplate> _templates;

    public InMemorySlabAssemblyTemplateCatalog()
        : this(CreateDefaultTemplates())
    {
    }

    public InMemorySlabAssemblyTemplateCatalog(IEnumerable<SlabAssemblyTemplate> templates)
    {
        _templates = templates.Select(CloneTemplate).ToList();
    }

    public IReadOnlyList<SlabAssemblyTemplate> GetAllTemplates() => _templates;

    public SlabAssemblyTemplate? GetById(string templateId)
        => _templates.FirstOrDefault(template =>
            string.Equals(template.TemplateId, templateId, StringComparison.OrdinalIgnoreCase));

    public void SaveAll(IReadOnlyCollection<SlabAssemblyTemplate> templates)
    {
        _templates.Clear();
        _templates.AddRange(templates.Select(CloneTemplate));
    }

    private static IEnumerable<SlabAssemblyTemplate> CreateDefaultTemplates()
    {
        yield return new SlabAssemblyTemplate
        {
            TemplateId = "slab-120-finish",
            TemplateName = "120结构板-上部面层",
            WallJunctionMode = SlabWallJunctionMode.StopAtWallFace,
            CoreRule = new SlabCoreRule
            {
                Name = "结构板",
                Thickness = 120,
                MaterialOrCategory = "结构",
                VisibleInSection = true
            },
            TopLayers = new List<SlabLayerRule>
            {
                new()
                {
                    Name = "找平层",
                    Side = SlabLayerSide.Top,
                    Order = 1,
                    Thickness = 20,
                    MaterialOrCategory = "找平",
                    VisibleInSection = true
                },
                new()
                {
                    Name = "装修面层",
                    Side = SlabLayerSide.Top,
                    Order = 2,
                    Thickness = 10,
                    MaterialOrCategory = "装修",
                    VisibleInSection = true
                }
            }
        };
    }

    private static SlabAssemblyTemplate CloneTemplate(SlabAssemblyTemplate template)
    {
        return new SlabAssemblyTemplate
        {
            TemplateId = template.TemplateId,
            TemplateName = template.TemplateName,
            WallJunctionMode = template.WallJunctionMode,
            CoreRule = new SlabCoreRule
            {
                Name = template.CoreRule.Name,
                Thickness = template.CoreRule.Thickness,
                MaterialOrCategory = template.CoreRule.MaterialOrCategory,
                VisibleInSection = template.CoreRule.VisibleInSection
            },
            TopLayers = template.TopLayers.Select(CloneRule).ToList(),
            BottomLayers = template.BottomLayers.Select(CloneRule).ToList()
        };
    }

    private static SlabLayerRule CloneRule(SlabLayerRule rule)
    {
        return new SlabLayerRule
        {
            Name = rule.Name,
            Side = rule.Side,
            Order = rule.Order,
            Thickness = rule.Thickness,
            MaterialOrCategory = rule.MaterialOrCategory,
            VisibleInSection = rule.VisibleInSection
        };
    }
}
