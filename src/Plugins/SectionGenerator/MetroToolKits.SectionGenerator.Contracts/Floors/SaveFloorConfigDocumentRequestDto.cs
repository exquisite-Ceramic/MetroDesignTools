using MetroToolKits.SectionGenerator.Contracts.Output;

namespace MetroToolKits.SectionGenerator.Contracts.Floors;

public sealed class SaveFloorConfigDocumentRequestDto
{
    public SaveFloorConfigRequestDto FloorConfig { get; init; } = new();

    public SectionOutputConfigDto OutputConfig { get; init; } = new();
}
