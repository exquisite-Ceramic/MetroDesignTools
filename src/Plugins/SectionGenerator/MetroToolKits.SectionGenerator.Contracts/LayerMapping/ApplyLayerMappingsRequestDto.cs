namespace MetroToolKits.SectionGenerator.Contracts.LayerMapping;

public sealed class ApplyLayerMappingsRequestDto
{
    public bool ApplyToEntireDrawing { get; init; }

    public IReadOnlyList<LayerTypeAssignmentDto> LayerAssignments { get; init; } = Array.Empty<LayerTypeAssignmentDto>();
}

public sealed class LayerTypeAssignmentDto
{
    public string SourceLayerName { get; init; } = string.Empty;

    public string TypeId { get; init; } = string.Empty;

    public string? TemplateId { get; init; }
}
