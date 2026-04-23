using MetroToolKits.Foundation.Building.Elements;
using MetroToolKits.Foundation.Core.Geometry;

namespace MetroToolKits.SectionGenerator.Core.Sections;

/// <summary>
/// 剖面组合器 - 核心算法层
/// 接收剖切线和构件列表，计算并返回局部剖面坐标中的 SectionGeometryData。
/// 主线稳定版不依赖复合墙/复合楼板模板体系。
/// </summary>
public sealed class SectionComposer
{
    private readonly FloorVerticalProfileBuilder _verticalProfileBuilder;

    public SectionComposer()
        : this(new FloorVerticalProfileBuilder())
    {
    }

    public SectionComposer(FloorVerticalProfileBuilder verticalProfileBuilder)
    {
        _verticalProfileBuilder = verticalProfileBuilder;
    }

    public SectionGeometryData Generate(
        Line3D sectionLine,
        Vector3D viewDirection,
        IEnumerable<BuildingElement> elements,
        FloorConfig floorConfig,
        double baseElevation = 0)
    {
        var projector = new SectionCoordinateProjector(sectionLine);
        var verticalProfile = _verticalProfileBuilder.Build(floorConfig, projector.SectionLength, baseElevation);
        var context = new SectionGeometryContext
        {
            SectionLine = sectionLine,
            ViewDirection = viewDirection,
            Projector = projector,
            VerticalProfile = verticalProfile
        };

        var elementList = elements.ToList();
        var slabElements = elementList.OfType<Slab>().Cast<BuildingElement>().ToList();
        var nonSlabElements = elementList.Except(slabElements).ToList();

        var elementDataList = new List<ElementSectionData>();
        foreach (var element in nonSlabElements)
        {
            var data = BuildNonSlabElementData(element, context);
            if (data == null || data.CutLineSegments.Count == 0)
            {
                continue;
            }

            elementDataList.Add(data);
        }

        var wallIntervals = elementDataList
            .Where(element => string.Equals(element.ElementType, "Wall", StringComparison.OrdinalIgnoreCase))
            .Select(element =>
            {
                var xValues = element.CutLineSegments
                    .Select(segment => segment.Line)
                    .SelectMany(line => new[] { line.Start.X, line.End.X })
                    .ToList();
                return xValues.Count == 0 ? ((double StartX, double EndX)?)null : (xValues.Min(), xValues.Max());
            })
            .Where(interval => interval.HasValue && interval.Value.EndX - interval.Value.StartX > 1e-6)
            .Select(interval => interval!.Value)
            .ToList();

        foreach (var slab in slabElements.OfType<Slab>())
        {
            var data = BuildLegacySlabElementData(slab, context, wallIntervals);
            if (data == null || data.CutLineSegments.Count == 0)
            {
                continue;
            }

            elementDataList.Add(data);
        }

        var slabLineSegments = _verticalProfileBuilder.BuildBoundaryLineSegments(floorConfig, verticalProfile, wallIntervals)
            .Where(segment => !IsDegenerate(segment.Line))
            .ToList();

        return new SectionGeometryData
        {
            FloorName = floorConfig.Name,
            BaseElevation = baseElevation,
            FloorHeight = floorConfig.Height,
            VerticalProfile = verticalProfile,
            Elements = elementDataList,
            SlabLines = slabLineSegments.Select(segment => segment.Line).ToList(),
            SlabLineSegments = slabLineSegments,
            HatchRegions = _verticalProfileBuilder.BuildBoundaryHatchRegions(floorConfig, verticalProfile)
        };
    }

    private static ElementSectionData? BuildNonSlabElementData(BuildingElement element, SectionGeometryContext context)
    {
        return element switch
        {
            Wall wall => BuildLegacyWallElementData(wall, context),
            Column column => BuildColumnElementData(column, context),
            _ => BuildGenericElementData(element, context)
        };
    }

    private static ElementSectionData? BuildLegacyWallElementData(Wall wall, SectionGeometryContext context)
    {
        var cutLines = wall.GetSectionGeometry(context)
            .Select(context.Projector.ProjectLine)
            .Where(line => !IsDegenerate(line))
            .ToList();
        if (cutLines.Count == 0)
        {
            return null;
        }

        var cutLineSegments = cutLines.Select(line => new SectionLineSegment
        {
            Line = line,
            Role = SectionLineRole.Structural
        }).ToList();

        var mainLine = cutLines[0];
        var halfThickness = Math.Max(wall.Thickness / 2.0, 1);
        var hatchRegions = new[]
        {
            new SectionHatchRegion
            {
                Category = SectionHatchCategory.Wall,
                Boundary = new[]
                {
                    new Point3D(mainLine.Start.X - halfThickness, mainLine.Start.Y, 0),
                    new Point3D(mainLine.Start.X + halfThickness, mainLine.Start.Y, 0),
                    new Point3D(mainLine.End.X + halfThickness, mainLine.End.Y, 0),
                    new Point3D(mainLine.End.X - halfThickness, mainLine.End.Y, 0)
                }
            }
        };

        return new ElementSectionData
        {
            SourceHandle = wall.SourceHandle ?? string.Empty,
            SourceHandles = wall.SourceHandles.Count > 0
                ? wall.SourceHandles
                : string.IsNullOrWhiteSpace(wall.SourceHandle)
                    ? Array.Empty<string>()
                    : new[] { wall.SourceHandle },
            ElementType = wall.ElementType,
            CutLines = cutLines,
            CutLineSegments = cutLineSegments,
            SightLines = Array.Empty<Line3D>(),
            HatchRegions = hatchRegions
        };
    }

    private static ElementSectionData? BuildColumnElementData(Column column, SectionGeometryContext context)
    {
        var worldLines = column.GetSectionGeometry(context).ToList();
        var cutLineSegments = worldLines
            .Select(context.Projector.ProjectLine)
            .Where(line => !IsDegenerate(line))
            .Select(line => new SectionLineSegment
            {
                Line = line,
                Role = SectionLineRole.Structural
            })
            .ToList();
        if (cutLineSegments.Count == 0)
        {
            return null;
        }

        var hatchRegions = new List<SectionHatchRegion>();
        if (cutLineSegments.Count >= 2)
        {
            var left = cutLineSegments[0].Line;
            var right = cutLineSegments[1].Line;
            hatchRegions.Add(new SectionHatchRegion
            {
                Category = SectionHatchCategory.Column,
                Boundary = new[]
                {
                    left.Start,
                    right.Start,
                    right.End,
                    left.End
                }
            });
        }

        return new ElementSectionData
        {
            SourceHandle = column.SourceHandle ?? string.Empty,
            SourceHandles = column.SourceHandles.Count > 0
                ? column.SourceHandles
                : string.IsNullOrWhiteSpace(column.SourceHandle)
                    ? Array.Empty<string>()
                    : new[] { column.SourceHandle },
            ElementType = column.ElementType,
            CutLines = cutLineSegments.Select(segment => segment.Line).ToList(),
            CutLineSegments = cutLineSegments,
            SightLines = Array.Empty<Line3D>(),
            HatchRegions = hatchRegions
        };
    }

    private static ElementSectionData? BuildLegacySlabElementData(
        Slab slab,
        SectionGeometryContext context,
        IReadOnlyList<(double StartX, double EndX)> wallIntervals)
    {
        if (!TryResolveSlabSpan(slab.Outline, context.SectionLine, out var left, out var right))
        {
            return null;
        }

        var topLine = context.Projector.ProjectLine(new Line3D(
            new Point3D(left.X, left.Y, slab.TopElevation),
            new Point3D(right.X, right.Y, slab.TopElevation)));
        var bottomLine = context.Projector.ProjectLine(new Line3D(
            new Point3D(left.X, left.Y, slab.TopElevation - slab.Thickness),
            new Point3D(right.X, right.Y, slab.TopElevation - slab.Thickness)));

        var topSegments = ClipAtWallFaces(topLine, wallIntervals)
            .Where(line => !IsDegenerate(line))
            .ToList();
        var bottomSegments = ClipAtWallFaces(bottomLine, wallIntervals)
            .Where(line => !IsDegenerate(line))
            .ToList();
        var cutLines = topSegments.Concat(bottomSegments).ToList();
        if (cutLines.Count == 0)
        {
            return null;
        }

        var cutLineSegments = topSegments.Concat(bottomSegments)
            .Select(line => new SectionLineSegment
            {
                Line = line,
                Role = SectionLineRole.Structural
            })
            .ToList();

        var hatchRegions = new[]
        {
            new SectionHatchRegion
            {
                Category = SectionHatchCategory.Slab,
                Boundary = new[]
                {
                    new Point3D(topLine.Start.X, bottomLine.Start.Y, 0),
                    new Point3D(topLine.End.X, bottomLine.End.Y, 0),
                    new Point3D(topLine.End.X, topLine.End.Y, 0),
                    new Point3D(topLine.Start.X, topLine.Start.Y, 0)
                }
            }
        };

        return new ElementSectionData
        {
            SourceHandle = slab.SourceHandle ?? string.Empty,
            SourceHandles = slab.SourceHandles.Count > 0
                ? slab.SourceHandles
                : string.IsNullOrWhiteSpace(slab.SourceHandle)
                    ? Array.Empty<string>()
                    : new[] { slab.SourceHandle },
            ElementType = slab.ElementType,
            CutLines = cutLines,
            CutLineSegments = cutLineSegments,
            SightLines = Array.Empty<Line3D>(),
            HatchRegions = hatchRegions
        };
    }

    private static ElementSectionData? BuildGenericElementData(BuildingElement element, SectionGeometryContext context)
    {
        var cutLines = element.GetSectionGeometry(context)
            .Select(context.Projector.ProjectLine)
            .Where(line => !IsDegenerate(line))
            .ToList();
        if (cutLines.Count == 0)
        {
            return null;
        }

        var cutLineSegments = cutLines.Select(line => new SectionLineSegment
        {
            Line = line,
            Role = SectionLineRole.Structural
        }).ToList();

        return new ElementSectionData
        {
            SourceHandle = element.SourceHandle ?? string.Empty,
            SourceHandles = element.SourceHandles.Count > 0
                ? element.SourceHandles
                : string.IsNullOrWhiteSpace(element.SourceHandle)
                    ? Array.Empty<string>()
                    : new[] { element.SourceHandle },
            ElementType = element.ElementType,
            CutLines = cutLines,
            CutLineSegments = cutLineSegments,
            SightLines = Array.Empty<Line3D>()
        };
    }

    private static bool TryResolveSlabSpan(
        IReadOnlyList<Point3D> outline,
        Line3D sectionLine,
        out Point3D left,
        out Point3D right)
    {
        left = default;
        right = default;

        if (outline.Count < 2)
        {
            return false;
        }

        var intersections = new List<Point3D>();
        for (int i = 0; i < outline.Count; i++)
        {
            var p1 = outline[i];
            var p2 = outline[(i + 1) % outline.Count];
            var intersection = LineIntersection2D(sectionLine, new Line3D(p1, p2));
            if (intersection.HasValue)
            {
                intersections.Add(intersection.Value);
            }
        }

        if (intersections.Count < 2)
        {
            return false;
        }

        var ordered = intersections
            .Select(point => new
            {
                Point = point,
                Chainage = SectionCoordinateProjector.GetChainage(sectionLine, point)
            })
            .OrderBy(item => item.Chainage)
            .ToList();

        var unique = new List<(Point3D Point, double Chainage)>();
        foreach (var point in ordered)
        {
            if (unique.Count == 0 || Math.Abs(unique[^1].Chainage - point.Chainage) > 1e-6)
            {
                unique.Add((point.Point, point.Chainage));
            }
        }

        if (unique.Count < 2)
        {
            return false;
        }

        left = unique.First().Point;
        right = unique.Last().Point;
        return true;
    }

    private static Point3D? LineIntersection2D(Line3D line1, Line3D line2)
    {
        var x1 = line1.Start.X;
        var y1 = line1.Start.Y;
        var x2 = line1.End.X;
        var y2 = line1.End.Y;
        var x3 = line2.Start.X;
        var y3 = line2.Start.Y;
        var x4 = line2.End.X;
        var y4 = line2.End.Y;

        var denom = (x1 - x2) * (y3 - y4) - (y1 - y2) * (x3 - x4);
        if (Math.Abs(denom) < 1e-10)
        {
            return null;
        }

        var t = ((x1 - x3) * (y3 - y4) - (y1 - y3) * (x3 - x4)) / denom;
        var u = -((x1 - x2) * (y1 - y3) - (y1 - y2) * (x1 - x3)) / denom;
        if (t < 0 || t > 1 || u < 0 || u > 1)
        {
            return null;
        }

        return new Point3D(
            x1 + t * (x2 - x1),
            y1 + t * (y2 - y1),
            line1.Start.Z + t * (line1.End.Z - line1.Start.Z));
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

    private static bool IsDegenerate(Line3D line, double tolerance = 1e-6)
        => line.Start.DistanceTo(line.End) <= tolerance;
}
