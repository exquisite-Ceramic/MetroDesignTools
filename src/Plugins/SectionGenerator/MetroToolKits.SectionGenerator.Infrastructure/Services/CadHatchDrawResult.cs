using MetroToolKits.SectionGenerator.Core.Sections;

namespace MetroToolKits.SectionGenerator.Infrastructure.Services;

public sealed class CadHatchDrawResult
{
    public bool Succeeded { get; init; }

    public SectionHatchCategory Category { get; init; }

    public string? PatternName { get; init; }

    public string? EffectivePatternName { get; init; }

    public string? LayerName { get; init; }

    public int BoundaryPointCount { get; init; }

    public string? Message { get; init; }

    public Exception? Exception { get; init; }
}
