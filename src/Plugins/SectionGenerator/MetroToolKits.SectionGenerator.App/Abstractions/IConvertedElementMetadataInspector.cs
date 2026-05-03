using MetroToolKits.SectionGenerator.App.Models;

namespace MetroToolKits.SectionGenerator.App.Abstractions;

public interface IConvertedElementMetadataInspector
{
    ConvertedElementMetadataInspectionResult InspectMissingWallTemplateMetadata();
}
