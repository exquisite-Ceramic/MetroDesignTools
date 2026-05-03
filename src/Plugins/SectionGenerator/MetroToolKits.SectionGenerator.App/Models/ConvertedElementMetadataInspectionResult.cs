namespace MetroToolKits.SectionGenerator.App.Models;

public sealed class ConvertedElementMetadataInspectionResult
{
    public static ConvertedElementMetadataInspectionResult Empty { get; } = new();

    public int ScannedEntityCount { get; init; }

    public int WallCandidateCount { get; init; }

    public IReadOnlyList<ConvertedElementMetadataIssue> Issues { get; init; } =
        Array.Empty<ConvertedElementMetadataIssue>();

    public bool HasIssues => Issues.Count > 0;
}
