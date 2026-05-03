namespace MetroToolKits.SectionGenerator.App.Models;

public sealed class ConvertedElementMetadataRepairResult
{
    public int RequestedCount { get; init; }

    public int RepairedCount { get; init; }

    public int FailedCount { get; init; }

    public IReadOnlyList<ConvertedElementMetadataRepairFailure> Failures { get; init; } =
        Array.Empty<ConvertedElementMetadataRepairFailure>();

    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();
}
