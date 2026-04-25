using MetroToolKits.SectionGenerator.App.Models;
using MetroToolKits.SectionGenerator.Contracts.Floors;

namespace MetroToolKits.SectionGenerator.App.Abstractions;

public interface IFloorConfigSaveRequestMapper
{
    SaveFloorConfigRequestDto ToRequest(LoadedSectionConfig document);

    void ApplyToDocument(SaveFloorConfigRequestDto request, LoadedSectionConfig document);
}
