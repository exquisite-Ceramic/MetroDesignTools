using MetroToolKits.Foundation.Building.Elements;
using MetroToolKits.Foundation.Building.Types;

namespace MetroToolKits.SectionGenerator.Core.Sections;

/// <summary>
/// 复合楼板装配器。
/// </summary>
public sealed class SlabAssemblyBuilder : ISlabAssemblyBuilder
{
    public CompositeSlabElement Build(CoreSlabArea coreArea, SlabAssemblyTemplate template)
    {
        var coreThickness = template.CoreRule.Thickness;
        var coreBottom = -coreThickness;
        var sections = new List<SlabLayerSection>
        {
            new()
            {
                Name = template.CoreRule.Name,
                MaterialOrCategory = template.CoreRule.MaterialOrCategory,
                VisibleInSection = template.CoreRule.VisibleInSection,
                IsCore = true,
                Thickness = coreThickness,
                TopOffset = 0,
                BottomOffset = coreBottom
            }
        };

        BuildSideSections(template.TopLayers, true, sections);
        BuildSideSections(template.BottomLayers, false, sections, coreBottom);

        return new CompositeSlabElement
        {
            TemplateId = template.TemplateId,
            TemplateName = template.TemplateName,
            WallJunctionMode = template.WallJunctionMode,
            CoreArea = coreArea,
            LayerSections = sections
                .OrderByDescending(layer => layer.TopOffset)
                .ThenByDescending(layer => layer.BottomOffset)
                .ToList(),
            SourceHandle = coreArea.SourceHandles.FirstOrDefault(),
            SourceHandles = coreArea.SourceHandles.ToList(),
            SourceLayer = coreArea.SourceLayer,
            Name = template.TemplateName
        };
    }

    private static void BuildSideSections(
        IEnumerable<SlabLayerRule> rules,
        bool isTop,
        ICollection<SlabLayerSection> sections,
        double startBottomOffset = 0)
    {
        var orderedRules = rules
            .Where(rule => rule.Thickness > 0)
            .OrderBy(rule => rule.Order)
            .ThenBy(rule => rule.Name, StringComparer.OrdinalIgnoreCase);

        if (isTop)
        {
            var cursor = 0d;
            foreach (var rule in orderedRules)
            {
                var next = cursor + rule.Thickness;
                sections.Add(new SlabLayerSection
                {
                    Name = rule.Name,
                    MaterialOrCategory = rule.MaterialOrCategory,
                    VisibleInSection = rule.VisibleInSection,
                    IsCore = false,
                    Side = SlabLayerSide.Top,
                    Thickness = rule.Thickness,
                    TopOffset = next,
                    BottomOffset = cursor
                });
                cursor = next;
            }

            return;
        }

        var bottomCursor = startBottomOffset;
        foreach (var rule in orderedRules)
        {
            var next = bottomCursor - rule.Thickness;
            sections.Add(new SlabLayerSection
            {
                Name = rule.Name,
                MaterialOrCategory = rule.MaterialOrCategory,
                VisibleInSection = rule.VisibleInSection,
                IsCore = false,
                Side = SlabLayerSide.Bottom,
                Thickness = rule.Thickness,
                TopOffset = bottomCursor,
                BottomOffset = next
            });
            bottomCursor = next;
        }
    }
}
