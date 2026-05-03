using MetroToolKits.Foundation.Building.Elements;
using MetroToolKits.Foundation.Core.Geometry;

namespace MetroToolKits.SectionGenerator.Core.Sections;

public sealed class SightLineComposer
{
    private const double Tolerance = 1e-6;

    public IReadOnlyList<ElementSectionData> Compose(
        IReadOnlyList<SectionSightLineCandidate> candidates,
        double baseElevation,
        SightLineComposerOptions? options = null)
    {
        options ??= new SightLineComposerOptions();
        if (candidates.Count == 0)
        {
            return Array.Empty<ElementSectionData>();
        }

        var result = new List<ElementSectionData>();
        foreach (var candidate in candidates)
        {
            if (!ShouldGenerateSightLines(candidate.Element, options))
            {
                continue;
            }

            var sightLines = BuildRectangle(candidate, baseElevation);
            if (sightLines.Count == 0)
            {
                continue;
            }

            result.Add(new ElementSectionData
            {
                SourceHandle = candidate.SourceHandle,
                SourceHandles = string.IsNullOrWhiteSpace(candidate.SourceHandle)
                    ? Array.Empty<string>()
                    : new[] { candidate.SourceHandle },
                ElementType = string.IsNullOrWhiteSpace(candidate.ElementType)
                    ? candidate.Element.ElementType
                    : candidate.ElementType,
                CutLines = Array.Empty<Line3D>(),
                CutLineSegments = Array.Empty<SectionLineSegment>(),
                SightLines = sightLines,
                HatchRegions = Array.Empty<SectionHatchRegion>()
            });
        }

        return result;
    }

    private static bool ShouldGenerateSightLines(BuildingElement element, SightLineComposerOptions options)
    {
        // 楼板/顶板/底板已经由剖切线、边界线和填充表达；视深矩形会和板边线重复，默认不生成楼板视深线。
        if (!options.Enabled)
        {
            return false;
        }

        return element switch
        {
            Slab or CompositeSlabElement => options.IncludeSlabs,
            Wall or CompositeWallElement => options.IncludeWalls,
            Column => options.IncludeColumns,
            _ => true
        };
    }

    private static IReadOnlyList<Line3D> BuildRectangle(
        SectionSightLineCandidate candidate,
        double baseElevation)
    {
        var minX = Math.Min(candidate.MinChainage, candidate.MaxChainage);
        var maxX = Math.Max(candidate.MinChainage, candidate.MaxChainage);
        if (maxX - minX <= Tolerance)
        {
            return Array.Empty<Line3D>();
        }

        if (!TryResolveVerticalRange(candidate.Element, baseElevation, out var minY, out var maxY) ||
            maxY - minY <= Tolerance)
        {
            return Array.Empty<Line3D>();
        }

        var bottomLeft = new Point3D(minX, minY, 0);
        var bottomRight = new Point3D(maxX, minY, 0);
        var topRight = new Point3D(maxX, maxY, 0);
        var topLeft = new Point3D(minX, maxY, 0);

        return new[]
        {
            new Line3D(bottomLeft, bottomRight),
            new Line3D(bottomRight, topRight),
            new Line3D(topRight, topLeft),
            new Line3D(topLeft, bottomLeft)
        };
    }

    private static bool TryResolveVerticalRange(
        BuildingElement element,
        double baseElevation,
        out double minY,
        out double maxY)
    {
        switch (element)
        {
            case Wall wall:
                minY = baseElevation + wall.BaseElevation;
                maxY = minY + wall.Height;
                return true;

            case Column column:
                minY = baseElevation + column.BaseElevation;
                maxY = minY + column.Height;
                return true;

            case Slab slab:
                minY = baseElevation + slab.TopElevation - slab.Thickness;
                maxY = baseElevation + slab.TopElevation;
                return true;

            case CompositeWallElement compositeWall:
                minY = baseElevation + compositeWall.CoreSegment.BaseElevation;
                maxY = minY + compositeWall.CoreSegment.Height;
                return true;

            case CompositeSlabElement compositeSlab:
                if (compositeSlab.LayerSections.Count == 0)
                {
                    minY = baseElevation + compositeSlab.CoreArea.CoreTopElevation;
                    maxY = minY;
                    return false;
                }

                minY = baseElevation + compositeSlab.CoreArea.CoreTopElevation +
                    compositeSlab.LayerSections.Min(layer => Math.Min(layer.TopOffset, layer.BottomOffset));
                maxY = baseElevation + compositeSlab.CoreArea.CoreTopElevation +
                    compositeSlab.LayerSections.Max(layer => Math.Max(layer.TopOffset, layer.BottomOffset));
                return true;

            default:
                return TryResolveBoundingBoxVerticalRange(element, baseElevation, out minY, out maxY);
        }
    }

    private static bool TryResolveBoundingBoxVerticalRange(
        BuildingElement element,
        double baseElevation,
        out double minY,
        out double maxY)
    {
        var vertices = element.GetBoundingBox()?.Vertices.ToList();
        if (vertices is not { Count: > 0 })
        {
            minY = 0;
            maxY = 0;
            return false;
        }

        minY = baseElevation + vertices.Min(point => point.Z);
        maxY = baseElevation + vertices.Max(point => point.Z);
        return true;
    }
}
