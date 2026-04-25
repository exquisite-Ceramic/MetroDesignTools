using MetroToolKits.SectionGenerator.App.Models;
using MetroToolKits.SectionGenerator.Contracts.Output;

namespace MetroToolKits.SectionGenerator.App.Abstractions;

public interface ISectionOutputConfigMapper
{
    SectionOutputConfigDto ToDto(SectionOutputConfig? outputConfig);

    SectionOutputConfig ToDomain(SectionOutputConfigDto dto);

    void ApplyToDocument(SectionOutputConfigDto dto, LoadedSectionConfig document);
}
