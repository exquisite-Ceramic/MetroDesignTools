using MetroToolKits.Foundation.Building.Elements;
using MetroToolKits.Foundation.Building.Types;
using MetroToolKits.Foundation.Core.Geometry;

namespace MetroToolKits.SectionGenerator.Core.Sections;

/// <summary>
/// 剖面组合器 - 核心算法层
/// 接收剖切线和构件列表，计算并返回局部剖面坐标中的 SectionGeometryData
/// </summary>
public sealed class SectionComposer
{
    private readonly FloorVerticalProfileBuilder _verticalProfileBuilder;

    public SectionComposer()
        : this(new FloorVerticalProfileBuilder(
            new InMemorySlabAssemblyTemplateCatalog(),
            new SlabAssemblyBuilder()))
    {
    }

    public SectionComposer(FloorVerticalProfileBuilder verticalProfileBuilder)
    {
        _verticalProfileBuilder = verticalProfileBuilder;
    }

    /// <summary>
    /// 生成单层剖面几何数据
    /// </summary>
    /// <param name="sectionLine">剖切线（平面坐标）</param>
    /// <param name="viewDirection">视图方向（看线方向）</param>
    /// <param name="elements">该层所有构件</param>
    /// <param name="floorConfig">楼层配置</param>
    /// <param name="baseElevation">楼层底部绝对标高</param>
    public SectionGeometryData Generate(
        Line3D sectionLine,
        Vector3D viewDirection,
        IEnumerable<BuildingElement> elements,
        FloorConfig floorConfig,
        double baseElevation = 0)
    {
        var elementDataList = new List<ElementSectionData>();
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
        var slabElements = elementList
            .Where(element => element is Slab or CompositeSlabElement)
            .ToList();
        var nonSlabElements = elementList
            .Where(element => element is not Slab && element is not CompositeSlabElement)
            .ToList();

        foreach (var element in nonSlabElements)
        {
            var elementData = BuildNonSlabElementData(element, context);
            if (elementData == null || elementData.CutLineSegments.Count == 0)
            {
                continue;
            }

            elementDataList.Add(elementData);
        }

        var wallIntervals = elementDataList
            .Where(element => string.Equals(element.ElementType, "Wall", StringComparison.OrdinalIgnoreCase))
            .Select(element =>
            {
                var xs = element.CutLines
                    .SelectMany(line => new[] { line.Start.X, line.End.X })
                    .ToList();
                return xs.Count == 0
                    ? ((double StartX, double EndX)?)null
                    : (xs.Min(), xs.Max());
            })
            .Where(interval => interval.HasValue && interval.Value.EndX - interval.Value.StartX > 1e-6)
            .Select(interval => interval!.Value)
            .ToList();

        foreach (var slabElement in slabElements)
        {
            var slabData = BuildDisplayedSlabElementData(slabElement, context, wallIntervals);
            if (slabData == null || slabData.CutLines.Count == 0)
            {
                continue;
            }

            elementDataList.Add(slabData);
        }

        // 生成楼层边界板剖面线（顶板/底板），输出到局部剖面坐标
        var slabLineSegments = _verticalProfileBuilder.BuildBoundaryLineSegments(floorConfig, verticalProfile, wallIntervals)
            .Where(segment => !IsDegenerate(segment.Line))
            .ToList();
        var slabLines = slabLineSegments.Select(segment => segment.Line).ToList();
        var hatchRegions = _verticalProfileBuilder.BuildBoundaryHatchRegions(floorConfig, verticalProfile);

        return new SectionGeometryData
        {
            FloorName = floorConfig.Name,
            BaseElevation = baseElevation,
            FloorHeight = floorConfig.Height,
            VerticalProfile = verticalProfile,
            Elements = elementDataList,
            SlabLines = slabLines,
            SlabLineSegments = slabLineSegments,
            HatchRegions = hatchRegions
        };
    }

    private static bool IsDegenerate(Line3D line, double tolerance = 1e-6)
        => line.Start.DistanceTo(line.End) <= tolerance;

    private static ElementSectionData? BuildDisplayedSlabElementData(
        BuildingElement element,
        SectionGeometryContext context,
        IReadOnlyList<(double StartX, double EndX)> wallIntervals)
    {
        return element switch
        {
            CompositeSlabElement compositeSlab => BuildCompositeSlabElementData(compositeSlab, context, wallIntervals),
            Slab slab => BuildLegacySlabElementData(slab, context),
            _ => null
        };
    }

    private static ElementSectionData? BuildLegacySlabElementData(Slab slab, SectionGeometryContext context)
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

        var cutLines = new[] { topLine, bottomLine }
            .Where(line => !IsDegenerate(line))
            .ToList();
        if (cutLines.Count == 0)
        {
            return null;
        }

        var cutLineSegments = cutLines
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

    private static ElementSectionData? BuildCompositeSlabElementData(
        CompositeSlabElement slab,
        SectionGeometryContext context,
        IReadOnlyList<(double StartX, double EndX)> wallIntervals)
    {
        if (!TryResolveSlabSpan(slab.CoreArea.Outline, context.SectionLine, out var left, out var right))
        {
            return null;
        }

        var cutLines = new List<Line3D>();
        var cutLineSegments = new List<SectionLineSegment>();
        var visibleCore = slab.LayerSections.FirstOrDefault(layer => layer.IsCore && layer.VisibleInSection);
        if (visibleCore != null)
        {
            var coreTopLines = ProjectDisplayedLine(
                left,
                right,
                slab.CoreArea.CoreTopElevation + GetUpperSurfaceOffset(visibleCore),
                context,
                clipAtWalls: false,
                wallIntervals).ToList();
            var coreBottomLines = ProjectDisplayedLine(
                left,
                right,
                slab.CoreArea.CoreTopElevation + GetLowerSurfaceOffset(visibleCore),
                context,
                clipAtWalls: false,
                wallIntervals).ToList();
            cutLines.AddRange(coreTopLines);
            cutLines.AddRange(coreBottomLines);
            cutLineSegments.AddRange(coreTopLines.Concat(coreBottomLines).Select(line => new SectionLineSegment
            {
                Line = line,
                Role = SectionLineRole.Structural
            }));
        }

        var topFinish = slab.LayerSections
            .Where(layer => !layer.IsCore && layer.VisibleInSection && layer.Side == SlabLayerSide.Top)
            .OrderByDescending(GetUpperSurfaceOffset)
            .FirstOrDefault();
        if (topFinish != null)
        {
            var topFinishLines = ProjectDisplayedLine(
                left,
                right,
                slab.CoreArea.CoreTopElevation + GetUpperSurfaceOffset(topFinish),
                context,
                clipAtWalls: slab.WallJunctionMode == SlabWallJunctionMode.StopAtWallFace,
                wallIntervals).ToList();
            cutLines.AddRange(topFinishLines);
            cutLineSegments.AddRange(topFinishLines.Select(line => new SectionLineSegment
            {
                Line = line,
                Role = SectionLineRole.Finish
            }));
        }

        var bottomFinish = slab.LayerSections
            .Where(layer => !layer.IsCore && layer.VisibleInSection && layer.Side == SlabLayerSide.Bottom)
            .OrderBy(GetLowerSurfaceOffset)
            .FirstOrDefault();
        if (bottomFinish != null)
        {
            var bottomFinishLines = ProjectDisplayedLine(
                left,
                right,
                slab.CoreArea.CoreTopElevation + GetLowerSurfaceOffset(bottomFinish),
                context,
                clipAtWalls: slab.WallJunctionMode == SlabWallJunctionMode.StopAtWallFace,
                wallIntervals).ToList();
            cutLines.AddRange(bottomFinishLines);
            cutLineSegments.AddRange(bottomFinishLines.Select(line => new SectionLineSegment
            {
                Line = line,
                Role = SectionLineRole.Finish
            }));
        }

        cutLines = cutLines
            .Where(line => !IsDegenerate(line))
            .ToList();
        if (cutLines.Count == 0)
        {
            return null;
        }

        return new ElementSectionData
        {
            SourceHandle = slab.SourceHandle ?? string.Empty,
            SourceHandles = slab.SourceHandles.Count > 0
                ? slab.SourceHandles
                : string.IsNullOrWhiteSpace(slab.SourceHandle)
                    ? Array.Empty<string>()
                    : new[] { slab.SourceHandle },
            ElementType = slab.ElementType,
            TemplateId = slab.TemplateId,
            CutLines = cutLines,
            CutLineSegments = cutLineSegments,
            SightLines = Array.Empty<Line3D>(),
            HatchRegions = visibleCore == null
                ? Array.Empty<SectionHatchRegion>()
                : new[]
                {
                    new SectionHatchRegion
                    {
                        Category = SectionHatchCategory.Slab,
                        Boundary = new[]
                        {
                            new Point3D(context.Projector.GetChainage(left), slab.CoreArea.CoreTopElevation + GetLowerSurfaceOffset(visibleCore), 0),
                            new Point3D(context.Projector.GetChainage(right), slab.CoreArea.CoreTopElevation + GetLowerSurfaceOffset(visibleCore), 0),
                            new Point3D(context.Projector.GetChainage(right), slab.CoreArea.CoreTopElevation + GetUpperSurfaceOffset(visibleCore), 0),
                            new Point3D(context.Projector.GetChainage(left), slab.CoreArea.CoreTopElevation + GetUpperSurfaceOffset(visibleCore), 0)
                        }
                    }
                }
        };
    }

    private static ElementSectionData? BuildNonSlabElementData(BuildingElement element, SectionGeometryContext context)
    {
        return element switch
        {
            CompositeWallElement compositeWall => BuildCompositeWallElementData(compositeWall, context),
            Column column => BuildColumnElementData(column, context),
            Wall wall => BuildLegacyWallElementData(wall, context),
            _ => BuildGenericElementData(element, context)
        };
    }

    private static ElementSectionData? BuildCompositeWallElementData(CompositeWallElement wall, SectionGeometryContext context)
    {
        var worldLines = wall.GetSectionGeometry(context).ToList();
        if (worldLines.Count == 0)
        {
            return null;
        }

        var visibleLayers = wall.LayerSections.Where(layer => layer.VisibleInSection).ToList();
        var cutLineSegments = new List<SectionLineSegment>();
        var hatchRegions = new List<SectionHatchRegion>();

        for (var i = 0; i < visibleLayers.Count; i++)
        {
            var startIndex = i * 4;
            if (startIndex + 3 >= worldLines.Count)
            {
                break;
            }

            var lineChunk = worldLines
                .Skip(startIndex)
                .Take(4)
                .Select(context.Projector.ProjectLine)
                .Where(line => !IsDegenerate(line))
                .ToList();

            if (lineChunk.Count == 0)
            {
                continue;
            }

            var role = IsStructuralWallLayer(visibleLayers[i])
                ? SectionLineRole.Structural
                : SectionLineRole.Finish;
            cutLineSegments.AddRange(lineChunk.Select(line => new SectionLineSegment
            {
                Line = line,
                Role = role
            }));

            var first = context.Projector.ProjectLine(worldLines[startIndex]);
            var second = context.Projector.ProjectLine(worldLines[startIndex + 1]);
            hatchRegions.Add(new SectionHatchRegion
            {
                Category = SectionHatchCategory.Wall,
                Boundary = new[]
                {
                    first.Start,
                    second.Start,
                    second.End,
                    first.End
                }
            });
        }

        if (cutLineSegments.Count == 0)
        {
            return null;
        }

        return new ElementSectionData
        {
            SourceHandle = wall.SourceHandle ?? string.Empty,
            SourceHandles = wall.SourceHandles.Count > 0
                ? wall.SourceHandles
                : string.IsNullOrWhiteSpace(wall.SourceHandle)
                    ? Array.Empty<string>()
                    : new[] { wall.SourceHandle },
            ElementType = wall.ElementType,
            TemplateId = wall.TemplateId,
            CutLines = cutLineSegments.Select(segment => segment.Line).ToList(),
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
        if (worldLines.Count >= 2)
        {
            var left = context.Projector.ProjectLine(worldLines[0]);
            var right = context.Projector.ProjectLine(worldLines[1]);
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

        var hatchRegions = new List<SectionHatchRegion>();
        var line = cutLines[0];
        var halfThickness = Math.Max(wall.Thickness / 2.0, 1);
        hatchRegions.Add(new SectionHatchRegion
        {
            Category = SectionHatchCategory.Wall,
            Boundary = new[]
            {
                new Point3D(line.Start.X - halfThickness, line.Start.Y, 0),
                new Point3D(line.Start.X + halfThickness, line.Start.Y, 0),
                new Point3D(line.End.X + halfThickness, line.End.Y, 0),
                new Point3D(line.End.X - halfThickness, line.End.Y, 0)
            }
        });

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

    private static IEnumerable<Line3D> ProjectDisplayedLine(
        Point3D left,
        Point3D right,
        double elevation,
        SectionGeometryContext context,
        bool clipAtWalls,
        IReadOnlyList<(double StartX, double EndX)> wallIntervals)
    {
        var projected = context.Projector.ProjectLine(new Line3D(
            new Point3D(left.X, left.Y, elevation),
            new Point3D(right.X, right.Y, elevation)));

        return clipAtWalls
            ? ClipAtWallFaces(projected, wallIntervals).Where(line => !IsDegenerate(line)).ToArray()
            : new[] { projected };
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
            var edge = new Line3D(p1, p2);
            var intersection = LineIntersection2D(sectionLine, edge);
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
        var x1 = line1.Start.X; var y1 = line1.Start.Y;
        var x2 = line1.End.X; var y2 = line1.End.Y;
        var x3 = line2.Start.X; var y3 = line2.Start.Y;
        var x4 = line2.End.X; var y4 = line2.End.Y;

        var denom = (x1 - x2) * (y3 - y4) - (y1 - y2) * (x3 - x4);
        if (Math.Abs(denom) < 1e-10) return null;

        var t = ((x1 - x3) * (y3 - y4) - (y1 - y3) * (x3 - x4)) / denom;
        var u = -((x1 - x2) * (y1 - y3) - (y1 - y2) * (x1 - x3)) / denom;

        if (t < 0 || t > 1 || u < 0 || u > 1) return null;

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
        return line.Start.Y + (line.End.Y - line.Start.Y) * t;
    }

    private static double GetUpperSurfaceOffset(SlabLayerSection layer)
        => Math.Max(layer.TopOffset, layer.BottomOffset);

    private static double GetLowerSurfaceOffset(SlabLayerSection layer)
        => Math.Min(layer.TopOffset, layer.BottomOffset);

    private static bool IsStructuralWallLayer(WallLayerSection layer)
    {
        if (string.IsNullOrWhiteSpace(layer.MaterialOrCategory))
        {
            return false;
        }

        return layer.MaterialOrCategory.Contains("结构", StringComparison.OrdinalIgnoreCase) ||
               layer.MaterialOrCategory.Contains("core", StringComparison.OrdinalIgnoreCase);
    }
}
