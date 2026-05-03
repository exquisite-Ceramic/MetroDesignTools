namespace MetroToolKits.SectionGenerator.App.Models;

public sealed class ConvertedElementMetadataRepairFailure
{
    public string Handle { get; init; } = string.Empty;

    public string? LayerName { get; init; }

    public string Code { get; init; } = string.Empty;

    public string Message { get; init; } = string.Empty;
}
