using MetroToolKits.SectionGenerator.App.Models;

namespace MetroToolKits.SectionGenerator.App.Abstractions;

public interface IConvertedElementMetadataRepairService
{
    ConvertedElementMetadataRepairResult RepairMissingWallTemplateMetadata(
        ConvertedElementMetadataRepairRequest request);
}
