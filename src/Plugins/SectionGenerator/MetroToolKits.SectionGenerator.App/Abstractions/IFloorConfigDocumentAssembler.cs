using MetroToolKits.SectionGenerator.App.Models;
using MetroToolKits.SectionGenerator.Contracts.Floors;

namespace MetroToolKits.SectionGenerator.App.Abstractions;

public interface IFloorConfigDocumentAssembler
{
    FloorConfigDocumentDto Assemble(LoadedSectionConfig document);
}
