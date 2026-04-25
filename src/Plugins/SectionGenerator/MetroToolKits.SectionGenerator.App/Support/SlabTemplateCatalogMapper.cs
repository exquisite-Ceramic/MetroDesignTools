using MetroToolKits.Foundation.Building.Types;
using MetroToolKits.SectionGenerator.App.Abstractions;
using MetroToolKits.SectionGenerator.Contracts.Templates;

namespace MetroToolKits.SectionGenerator.App.Support;

public sealed class SlabTemplateCatalogMapper : ISlabTemplateCatalogMapper
{
    public SlabTemplateCatalogDto ToDto(IReadOnlyCollection<SlabAssemblyTemplate> templates)
    {
        ArgumentNullException.ThrowIfNull(templates);

        return new SlabTemplateCatalogDto
        {
            Templates = templates.Select(ToDto).ToList()
        };
    }

    public SlabAssemblyTemplateDto ToDto(SlabAssemblyTemplate template)
    {
        ArgumentNullException.ThrowIfNull(template);
        var coreRule = template.CoreRule ?? new SlabCoreRule();
        var topLayers = template.TopLayers ?? [];
        var bottomLayers = template.BottomLayers ?? [];

        return new SlabAssemblyTemplateDto
        {
            TemplateId = template.TemplateId,
            TemplateName = template.TemplateName,
            WallJunctionMode = template.WallJunctionMode.ToString(),
            CoreRule = new SlabCoreRuleDto
            {
                Name = coreRule.Name,
                Thickness = coreRule.Thickness,
                MaterialOrCategory = coreRule.MaterialOrCategory,
                VisibleInSection = coreRule.VisibleInSection
            },
            TopLayers = topLayers.Select(ToDto).ToList(),
            BottomLayers = bottomLayers.Select(ToDto).ToList()
        };
    }

    public IReadOnlyList<SlabAssemblyTemplate> ToDomain(SlabTemplateCatalogDto catalogDto)
    {
        ArgumentNullException.ThrowIfNull(catalogDto);

        return (catalogDto.Templates ?? []).Select(ToDomain).ToArray();
    }

    public SlabAssemblyTemplate ToDomain(SlabAssemblyTemplateDto templateDto)
    {
        ArgumentNullException.ThrowIfNull(templateDto);

        var defaultTemplate = new SlabAssemblyTemplate();
        var defaultCoreRule = new SlabCoreRule();

        return new SlabAssemblyTemplate
        {
            TemplateId = templateDto.TemplateId ?? string.Empty,
            TemplateName = templateDto.TemplateName ?? string.Empty,
            WallJunctionMode = ParseEnum(templateDto.WallJunctionMode, defaultTemplate.WallJunctionMode),
            CoreRule = new SlabCoreRule
            {
                Name = templateDto.CoreRule?.Name ?? defaultCoreRule.Name,
                Thickness = templateDto.CoreRule?.Thickness ?? defaultCoreRule.Thickness,
                MaterialOrCategory = templateDto.CoreRule?.MaterialOrCategory ?? defaultCoreRule.MaterialOrCategory,
                VisibleInSection = templateDto.CoreRule?.VisibleInSection ?? defaultCoreRule.VisibleInSection
            },
            TopLayers = (templateDto.TopLayers ?? []).Select(ToDomain).ToList(),
            BottomLayers = (templateDto.BottomLayers ?? []).Select(ToDomain).ToList()
        };
    }

    private static SlabLayerRuleDto ToDto(SlabLayerRule rule)
    {
        ArgumentNullException.ThrowIfNull(rule);

        return new SlabLayerRuleDto
        {
            Name = rule.Name,
            Side = rule.Side.ToString(),
            Order = rule.Order,
            Thickness = rule.Thickness,
            MaterialOrCategory = rule.MaterialOrCategory,
            VisibleInSection = rule.VisibleInSection
        };
    }

    private static SlabLayerRule ToDomain(SlabLayerRuleDto ruleDto)
    {
        ArgumentNullException.ThrowIfNull(ruleDto);

        var defaultRule = new SlabLayerRule();
        return new SlabLayerRule
        {
            Name = ruleDto.Name ?? string.Empty,
            Side = ParseEnum(ruleDto.Side, defaultRule.Side),
            Order = ruleDto.Order,
            Thickness = ruleDto.Thickness,
            MaterialOrCategory = ruleDto.MaterialOrCategory ?? string.Empty,
            VisibleInSection = ruleDto.VisibleInSection
        };
    }

    private static TEnum ParseEnum<TEnum>(string? value, TEnum fallback)
        where TEnum : struct, Enum
        => Enum.TryParse<TEnum>(value, ignoreCase: true, out var parsed)
            ? parsed
            : fallback;
}
