using MetroToolKits.Foundation.Core.Geometry;
using MetroToolKits.SectionGenerator.Core.Sections;

namespace MetroToolKits.SectionGenerator.Core.Sections.Hatching;

public static class SectionHatchRegionFactory
{
    public static bool TryCreate(
        SectionHatchCategory category,
        IEnumerable<Point3D> boundary,
        out SectionHatchRegion? region,
        out HatchBoundaryValidationResult validation)
    {
        validation = HatchBoundaryNormalizer.Normalize(boundary);
        if (!validation.IsValid)
        {
            region = null;
            return false;
        }

        region = new SectionHatchRegion
        {
            Category = category,
            Boundary = validation.NormalizedBoundary
        };
        return true;
    }

    public static bool TryCreateQuad(
        SectionHatchCategory category,
        Point3D p1,
        Point3D p2,
        Point3D p3,
        Point3D p4,
        out SectionHatchRegion? region,
        out HatchBoundaryValidationResult validation)
        => TryCreate(category, new[] { p1, p2, p3, p4 }, out region, out validation);
}
