namespace MetroToolKits.SectionGenerator.App.Models;

public sealed class ConvertedElementMetadataRepairRequest
{
    public IReadOnlyList<string> Handles { get; init; } = Array.Empty<string>();

    public string TemplateId { get; init; } = string.Empty;

    public string ConvertedType { get; init; } = "Wall";

    public bool OverwriteTemplateId { get; init; }
}
