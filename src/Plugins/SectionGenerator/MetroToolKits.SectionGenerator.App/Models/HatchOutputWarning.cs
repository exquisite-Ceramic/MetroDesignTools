using MetroToolKits.SectionGenerator.Core.Sections;

namespace MetroToolKits.SectionGenerator.App.Models;

public sealed class HatchOutputWarning
{
    public SectionHatchCategory Category { get; init; }

    public string Code { get; init; } = string.Empty;

    public string Message { get; init; } = string.Empty;

    public string? PatternName { get; init; }

    public string? EffectivePatternName { get; init; }

    public string? LayerName { get; init; }

    public int BoundaryPointCount { get; init; }
}
