using MetroToolKits.Foundation.Building.Elements;

namespace MetroToolKits.SectionGenerator.Core.Sections;

/// <summary>
/// Recognition data for one floor after cut elements and view-depth candidates have been separated.
/// </summary>
public sealed class SectionFloorRecognitionData
{
    public required FloorConfig Floor { get; init; }
    public IReadOnlyList<BuildingElement> CutElements { get; init; } = Array.Empty<BuildingElement>();
    public IReadOnlyList<SectionSightLineCandidate> SightLineCandidates { get; init; } = Array.Empty<SectionSightLineCandidate>();
}

/// <summary>
/// Core-layer candidate for future sight-line projection. Stage A only carries the data; it does not compose lines.
/// </summary>
public sealed class SectionSightLineCandidate
{
    public required BuildingElement Element { get; init; }
    public string SourceHandle { get; init; } = string.Empty;
    public string ElementType { get; init; } = string.Empty;
    public double MinChainage { get; init; }
    public double MaxChainage { get; init; }
    public double MinDepth { get; init; }
    public double MaxDepth { get; init; }
}
