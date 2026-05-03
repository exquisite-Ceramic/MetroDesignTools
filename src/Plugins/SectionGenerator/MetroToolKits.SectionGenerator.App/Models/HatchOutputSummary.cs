namespace MetroToolKits.SectionGenerator.App.Models;

public sealed class HatchOutputSummary
{
    public static HatchOutputSummary Empty { get; } = new();

    public int RequestedCount { get; init; }

    public int CreatedCount { get; init; }

    public int SkippedCount { get; init; }

    public int FallbackPatternCount { get; init; }

    public IReadOnlyList<HatchOutputWarning> Warnings { get; init; } = Array.Empty<HatchOutputWarning>();
}
