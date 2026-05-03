using MetroToolKits.Foundation.Core.Geometry;

namespace MetroToolKits.SectionGenerator.Core.Sections.Hatching;

public sealed record HatchBoundaryValidationResult(
    bool IsValid,
    HatchBoundaryIssueCode IssueCode,
    string Message,
    IReadOnlyList<Point3D> NormalizedBoundary);
