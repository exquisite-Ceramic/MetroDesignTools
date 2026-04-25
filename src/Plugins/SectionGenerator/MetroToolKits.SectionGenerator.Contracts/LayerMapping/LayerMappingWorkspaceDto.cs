using MetroToolKits.SectionGenerator.Contracts.Templates;

namespace MetroToolKits.SectionGenerator.Contracts.LayerMapping;

public sealed class LayerMappingWorkspaceDto
{
    public IReadOnlyList<LayerInfoDto> Layers { get; init; } = Array.Empty<LayerInfoDto>();

    public IReadOnlyList<ElementTypeOptionDto> ElementTypes { get; init; } = Array.Empty<ElementTypeOptionDto>();

    public IReadOnlyList<AssemblyTemplateOptionDto> WallTemplateOptions { get; init; } = Array.Empty<AssemblyTemplateOptionDto>();

    public IReadOnlyList<AssemblyTemplateOptionDto> SlabTemplateOptions { get; init; } = Array.Empty<AssemblyTemplateOptionDto>();

    public IReadOnlyList<LayerMappingDto> Mappings { get; init; } = Array.Empty<LayerMappingDto>();

    public string SummaryText { get; init; } = string.Empty;
}

public sealed class LayerInfoDto
{
    public string LayerName { get; init; } = string.Empty;
}

public sealed class ElementTypeOptionDto
{
    public string TypeId { get; init; } = string.Empty;

    public string TypeName { get; init; } = string.Empty;

    public string ShortcutKey { get; init; } = string.Empty;

    public string TargetLayerPrefix { get; init; } = string.Empty;

    public short ColorIndex { get; init; }

    public bool IsEnabled { get; init; }
}

public sealed class LayerMappingDto
{
    public string LayerName { get; init; } = string.Empty;

    public string TypeName { get; init; } = string.Empty;

    public string TypeId { get; init; } = string.Empty;

    public string TargetLayerPrefix { get; init; } = string.Empty;

    public short ColorIndex { get; init; }

    public string? TemplateId { get; init; }

    public string? TemplateName { get; init; }
}
