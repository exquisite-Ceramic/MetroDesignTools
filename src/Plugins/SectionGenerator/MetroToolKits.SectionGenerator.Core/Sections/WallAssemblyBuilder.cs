using MetroToolKits.Foundation.Building.Elements;
using MetroToolKits.Foundation.Building.Types;

namespace MetroToolKits.SectionGenerator.Core.Sections;

/// <summary>
/// 复合墙体构造器。
/// </summary>
public sealed class WallAssemblyBuilder : IWallAssemblyBuilder
{
    public CompositeWallElement Build(CoreWallSegment coreSegment, WallAssemblyTemplate template)
    {
        var sections = new List<WallLayerSection>();

        var halfCore = coreSegment.Thickness / 2.0;
        sections.Add(new WallLayerSection
        {
            Name = template.CoreRule.Name,
            MaterialOrCategory = template.CoreRule.MaterialOrCategory,
            VisibleInSection = template.CoreRule.VisibleInSection,
            Thickness = coreSegment.Thickness,
            InnerOffset = -halfCore,
            OuterOffset = halfCore
        });

        BuildSideSections(template.LeftLayers, -halfCore, growNegative: true, sections);
        BuildSideSections(template.RightLayers, halfCore, growNegative: false, sections);

        return new TemplatedCompositeWallElement
        {
            TemplateId = template.TemplateId,
            TemplateName = template.TemplateName,
            VerticalAnchorMode = template.VerticalAnchorMode,
            CoreSegment = coreSegment,
            LayerSections = sections
                .OrderBy(layer => Math.Min(layer.InnerOffset, layer.OuterOffset))
                .ThenBy(layer => Math.Max(layer.InnerOffset, layer.OuterOffset))
                .ToList(),
            SourceHandle = coreSegment.SourceHandles.FirstOrDefault(),
            SourceHandles = coreSegment.SourceHandles.ToList(),
            SourceLayer = coreSegment.SourceLayer,
            Name = template.TemplateName
        };
    }

    private static void BuildSideSections(
        IEnumerable<WallLayerRule> rules,
        double startOffset,
        bool growNegative,
        ICollection<WallLayerSection> sections)
    {
        var cursor = startOffset;
        foreach (var rule in rules
                     .Where(rule => rule.Thickness > 0)
                     .OrderBy(rule => rule.Order)
                     .ThenBy(rule => rule.Name, StringComparer.OrdinalIgnoreCase))
        {
            var next = growNegative ? cursor - rule.Thickness : cursor + rule.Thickness;
            sections.Add(new WallLayerSection
            {
                Name = rule.Name,
                MaterialOrCategory = rule.MaterialOrCategory,
                VisibleInSection = rule.VisibleInSection,
                Thickness = rule.Thickness,
                InnerOffset = cursor,
                OuterOffset = next
            });
            cursor = next;
        }
    }
}
