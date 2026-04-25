using MetroToolKits.SectionGenerator.Contracts.LayerMapping;

namespace MetroToolKits.SectionGenerator.App.Abstractions;

public interface ILayerMappingWorkspaceAssembler
{
    LayerMappingWorkspaceDto Assemble();
}
