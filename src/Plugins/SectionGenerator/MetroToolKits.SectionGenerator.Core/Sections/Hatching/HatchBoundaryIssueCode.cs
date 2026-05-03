namespace MetroToolKits.SectionGenerator.Core.Sections.Hatching;

public enum HatchBoundaryIssueCode
{
    None,
    TooFewPoints,
    TooFewDistinctPoints,
    NonFiniteCoordinate,
    DuplicatePoint,
    DegenerateEdge,
    ZeroArea,
    SelfIntersection
}
