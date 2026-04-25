using MetroToolKits.SectionGenerator.App.Abstractions;
using MetroToolKits.SectionGenerator.App.UseCases;
using MetroToolKits.SectionGenerator.Contracts.LayerMapping;

namespace MetroToolKits.SectionGenerator.App.Support;

public sealed class LayerMappingApplyRequestMapper : ILayerMappingApplyRequestMapper
{
    public ApplyLayerMappingsRequestDto ToRequest(
        IReadOnlyCollection<LayerMappingDto> mappings,
        bool applyToEntireDrawing)
    {
        ArgumentNullException.ThrowIfNull(mappings);

        return new ApplyLayerMappingsRequestDto
        {
            ApplyToEntireDrawing = applyToEntireDrawing,
            LayerAssignments = mappings
                .Select(static mapping => new LayerTypeAssignmentDto
                {
                    SourceLayerName = mapping.LayerName,
                    TypeId = mapping.TypeId,
                    TemplateId = mapping.TemplateId
                })
                .ToArray()
        };
    }

    public ElementConversionApplyRequest ToDomainRequest(ApplyLayerMappingsRequestDto request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return new ElementConversionApplyRequest
        {
            ApplyToEntireDrawing = request.ApplyToEntireDrawing,
            LayerMappings = request.LayerAssignments
                .Select(static assignment => new LayerTypeAssignment
                {
                    SourceLayerName = assignment.SourceLayerName,
                    TypeId = assignment.TypeId,
                    TemplateId = assignment.TemplateId
                })
                .ToArray()
        };
    }
}
