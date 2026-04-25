using MetroToolKits.SectionGenerator.App.UseCases;
using MetroToolKits.SectionGenerator.Contracts.LayerMapping;

namespace MetroToolKits.SectionGenerator.App.Abstractions;

public interface ILayerMappingApplyRequestMapper
{
    ApplyLayerMappingsRequestDto ToRequest(
        IReadOnlyCollection<LayerMappingDto> mappings,
        bool applyToEntireDrawing);

    ElementConversionApplyRequest ToDomainRequest(ApplyLayerMappingsRequestDto request);
}
