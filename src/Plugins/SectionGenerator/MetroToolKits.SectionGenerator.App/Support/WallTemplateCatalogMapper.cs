using MetroToolKits.Foundation.Building.Types;
using MetroToolKits.SectionGenerator.App.Abstractions;
using MetroToolKits.SectionGenerator.Contracts.Templates;

namespace MetroToolKits.SectionGenerator.App.Support;

public sealed class WallTemplateCatalogMapper : IWallTemplateCatalogMapper
{
    public WallTemplateCatalogDto ToDto(IReadOnlyCollection<WallAssemblyTemplate> templates)
    {
        ArgumentNullException.ThrowIfNull(templates);

        return new WallTemplateCatalogDto
        {
            Templates = templates.Select(ToDto).ToList()
        };
    }

    public WallAssemblyTemplateDto ToDto(WallAssemblyTemplate template)
    {
        ArgumentNullException.ThrowIfNull(template);
        var coreRule = template.CoreRule ?? new WallCoreRule();
        var leftLayers = template.LeftLayers ?? [];
        var rightLayers = template.RightLayers ?? [];

        return new WallAssemblyTemplateDto
        {
            TemplateId = template.TemplateId,
            TemplateName = template.TemplateName,
            VerticalAnchorMode = template.VerticalAnchorMode.ToString(),
            CoreRule = new WallCoreRuleDto
            {
                Name = coreRule.Name,
                Thickness = coreRule.Thickness,
                MaterialOrCategory = coreRule.MaterialOrCategory,
                VisibleInSection = coreRule.VisibleInSection,
                RecognitionMode = coreRule.RecognitionMode.ToString()
            },
            LeftLayers = leftLayers.Select(ToDto).ToList(),
            RightLayers = rightLayers.Select(ToDto).ToList()
        };
    }

    public IReadOnlyList<WallAssemblyTemplate> ToDomain(WallTemplateCatalogDto catalogDto)
    {
        ArgumentNullException.ThrowIfNull(catalogDto);

        return (catalogDto.Templates ?? []).Select(ToDomain).ToArray();
    }

    public WallAssemblyTemplate ToDomain(WallAssemblyTemplateDto templateDto)
    {
        ArgumentNullException.ThrowIfNull(templateDto);

        var defaultTemplate = new WallAssemblyTemplate();
        var defaultCoreRule = new WallCoreRule();

        return new WallAssemblyTemplate
        {
            TemplateId = templateDto.TemplateId ?? string.Empty,
            TemplateName = templateDto.TemplateName ?? string.Empty,
            VerticalAnchorMode = ParseEnum(templateDto.VerticalAnchorMode, defaultTemplate.VerticalAnchorMode),
            CoreRule = new WallCoreRule
            {
                Name = templateDto.CoreRule?.Name ?? defaultCoreRule.Name,
                Thickness = templateDto.CoreRule?.Thickness ?? defaultCoreRule.Thickness,
                MaterialOrCategory = templateDto.CoreRule?.MaterialOrCategory ?? defaultCoreRule.MaterialOrCategory,
                VisibleInSection = templateDto.CoreRule?.VisibleInSection ?? defaultCoreRule.VisibleInSection,
                RecognitionMode = ParseEnum(templateDto.CoreRule?.RecognitionMode, defaultCoreRule.RecognitionMode)
            },
            LeftLayers = (templateDto.LeftLayers ?? []).Select(ToDomain).ToList(),
            RightLayers = (templateDto.RightLayers ?? []).Select(ToDomain).ToList()
        };
    }

    private static WallLayerRuleDto ToDto(WallLayerRule rule)
    {
        ArgumentNullException.ThrowIfNull(rule);

        return new WallLayerRuleDto
        {
            Name = rule.Name,
            Side = rule.Side.ToString(),
            Order = rule.Order,
            Thickness = rule.Thickness,
            MaterialOrCategory = rule.MaterialOrCategory,
            VisibleInSection = rule.VisibleInSection
        };
    }

    private static WallLayerRule ToDomain(WallLayerRuleDto ruleDto)
    {
        ArgumentNullException.ThrowIfNull(ruleDto);

        var defaultRule = new WallLayerRule();
        return new WallLayerRule
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
