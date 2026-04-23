using MetroToolKits.Foundation.Core.Geometry;

namespace MetroToolKits.SectionGenerator.Core.Sections;

/// <summary>
/// 基于楼层厚度、装修厚度和顶/底板坡度构建楼层竖向轮廓。
/// 主线稳定版不依赖楼板模板体系。
/// </summary>
public sealed class FloorVerticalProfileBuilder
{
    public FloorVerticalProfile Build(
        FloorConfig floor,
        double sectionLength,
        double baseElevation)
    {
        var topSlope = ResolveTopSlope(floor);
        var bottomSlope = ResolveBottomSlope(floor);

        var bottomStructuralTopStart = baseElevation;
        var bottomStructuralTopEnd = baseElevation + (sectionLength * bottomSlope);
        var bottomStructuralBottomStart = bottomStructuralTopStart - floor.BottomSlabThickness;
        var bottomStructuralBottomEnd = bottomStructuralTopEnd - floor.BottomSlabThickness;

        var topStructuralBottomStart = baseElevation + floor.Height;
        var topStructuralBottomEnd = topStructuralBottomStart + (sectionLength * topSlope);
        var topStructuralTopStart = topStructuralBottomStart + floor.TopSlabThickness;
        var topStructuralTopEnd = topStructuralBottomEnd + floor.TopSlabThickness;

        var topBoundaryTopStart = topStructuralTopStart + floor.FinishThickness;
        var topBoundaryTopEnd = topStructuralTopEnd + floor.FinishThickness;

        return new FloorVerticalProfile
        {
            SectionLength = sectionLength,
            BottomStructuralBottom = new ProfileEdge(0, sectionLength, bottomStructuralBottomStart, bottomStructuralBottomEnd),
            BottomStructuralTop = new ProfileEdge(0, sectionLength, bottomStructuralTopStart, bottomStructuralTopEnd),
            BottomBoundaryBottom = new ProfileEdge(0, sectionLength, bottomStructuralBottomStart, bottomStructuralBottomEnd),
            BottomBoundaryTop = new ProfileEdge(0, sectionLength, bottomStructuralTopStart, bottomStructuralTopEnd),
            TopStructuralBottom = new ProfileEdge(0, sectionLength, topStructuralBottomStart, topStructuralBottomEnd),
            TopStructuralTop = new ProfileEdge(0, sectionLength, topStructuralTopStart, topStructuralTopEnd),
            TopBoundaryBottom = new ProfileEdge(0, sectionLength, topStructuralBottomStart, topStructuralBottomEnd),
            TopBoundaryTop = new ProfileEdge(0, sectionLength, topBoundaryTopStart, topBoundaryTopEnd)
        };
    }

    public IReadOnlyList<Line3D> BuildBoundaryLines(
        FloorConfig floor,
        FloorVerticalProfile profile,
        IReadOnlyList<(double StartX, double EndX)> wallIntervals)
        => BuildBoundaryLineSegments(floor, profile, wallIntervals)
            .Select(segment => segment.Line)
            .ToList();

    public IReadOnlyList<SectionLineSegment> BuildBoundaryLineSegments(
        FloorConfig floor,
        FloorVerticalProfile profile,
        IReadOnlyList<(double StartX, double EndX)> wallIntervals)
    {
        var segments = new List<SectionLineSegment>
        {
            CreateStructuralSegment(profile.BottomStructuralTop.ToLine3D()),
            CreateStructuralSegment(profile.BottomStructuralBottom.ToLine3D()),
            CreateStructuralSegment(profile.TopStructuralBottom.ToLine3D()),
            CreateStructuralSegment(profile.TopStructuralTop.ToLine3D()),
            CreateStructuralSegment(new Line3D(
                new Point3D(0, profile.BottomBoundaryBottom.StartY, 0),
                new Point3D(0, profile.TopBoundaryTop.StartY, 0))),
            CreateStructuralSegment(new Line3D(
                new Point3D(profile.SectionLength, profile.BottomBoundaryBottom.EndY, 0),
                new Point3D(profile.SectionLength, profile.TopBoundaryTop.EndY, 0)))
        };

        if (floor.FinishThickness > 1e-6)
        {
            var finishLine = profile.TopBoundaryTop.ToLine3D();
            segments.AddRange(ClipAtWallFaces(finishLine, wallIntervals).Select(line => new SectionLineSegment
            {
                Line = line,
                Role = SectionLineRole.Finish
            }));
        }

        return segments
            .Where(segment => segment.Line.Start.DistanceTo(segment.Line.End) > 1e-6)
            .ToList();
    }

    public IReadOnlyList<SectionHatchRegion> BuildBoundaryHatchRegions(
        FloorConfig floor,
        FloorVerticalProfile profile)
    {
        var regions = new List<SectionHatchRegion>();
        TryAddStructuralBoundaryHatch(
            regions,
            profile.BottomStructuralBottom.StartY,
            profile.BottomStructuralBottom.EndY,
            profile.BottomStructuralTop.StartY,
            profile.BottomStructuralTop.EndY,
            profile.SectionLength);
        TryAddStructuralBoundaryHatch(
            regions,
            profile.TopStructuralBottom.StartY,
            profile.TopStructuralBottom.EndY,
            profile.TopStructuralTop.StartY,
            profile.TopStructuralTop.EndY,
            profile.SectionLength);
        return regions;
    }

    private static double ResolveTopSlope(FloorConfig floor)
    {
        if (floor.TopBoundarySlab.SlopeEnabled)
        {
            return floor.TopBoundarySlab.SlopeValue;
        }

        if (floor.HasSlope)
        {
            return floor.SlopeValue;
        }

        return 0d;
    }

    private static double ResolveBottomSlope(FloorConfig floor)
        => floor.BottomBoundarySlab.SlopeEnabled
            ? floor.BottomBoundarySlab.SlopeValue
            : 0d;

    private static SectionLineSegment CreateStructuralSegment(Line3D line)
        => new()
        {
            Line = line,
            Role = SectionLineRole.Structural
        };

    private static void TryAddStructuralBoundaryHatch(
        ICollection<SectionHatchRegion> regions,
        double bottomStart,
        double bottomEnd,
        double topStart,
        double topEnd,
        double sectionLength)
    {
        if (Math.Abs(topStart - bottomStart) <= 1e-6 &&
            Math.Abs(topEnd - bottomEnd) <= 1e-6)
        {
            return;
        }

        regions.Add(new SectionHatchRegion
        {
            Category = SectionHatchCategory.Slab,
            Boundary = new[]
            {
                new Point3D(0, bottomStart, 0),
                new Point3D(sectionLength, bottomEnd, 0),
                new Point3D(sectionLength, topEnd, 0),
                new Point3D(0, topStart, 0)
            }
        });
    }

    private static IEnumerable<Line3D> ClipAtWallFaces(Line3D line, IReadOnlyList<(double StartX, double EndX)> wallIntervals)
    {
        if (wallIntervals.Count == 0)
        {
            yield return line;
            yield break;
        }

        var segments = new List<(double Start, double End)> { (line.Start.X, line.End.X) };
        foreach (var interval in wallIntervals)
        {
            var next = new List<(double Start, double End)>();
            foreach (var segment in segments)
            {
                var segStart = Math.Min(segment.Start, segment.End);
                var segEnd = Math.Max(segment.Start, segment.End);
                var clipStart = Math.Max(segStart, interval.StartX);
                var clipEnd = Math.Min(segEnd, interval.EndX);

                if (clipEnd <= clipStart + 1e-6)
                {
                    next.Add(segment);
                    continue;
                }

                if (clipStart > segStart + 1e-6)
                {
                    next.Add((segStart, clipStart));
                }

                if (clipEnd < segEnd - 1e-6)
                {
                    next.Add((clipEnd, segEnd));
                }
            }

            segments = next;
        }

        foreach (var segment in segments.Where(segment => segment.End - segment.Start > 1e-6))
        {
            var startY = InterpolateY(line, segment.Start);
            var endY = InterpolateY(line, segment.End);
            yield return new Line3D(
                new Point3D(segment.Start, startY, 0),
                new Point3D(segment.End, endY, 0));
        }
    }

    private static double InterpolateY(Line3D line, double x)
    {
        if (Math.Abs(line.End.X - line.Start.X) <= 1e-9)
        {
            return line.Start.Y;
        }

        var t = (x - line.Start.X) / (line.End.X - line.Start.X);
        return line.Start.Y + ((line.End.Y - line.Start.Y) * t);
    }
}
