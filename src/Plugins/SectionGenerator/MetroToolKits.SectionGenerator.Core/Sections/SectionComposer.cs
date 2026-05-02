using MetroToolKits.Foundation.Building.Elements;
using MetroToolKits.Foundation.Building.Types;
using MetroToolKits.Foundation.Core.Geometry;

namespace MetroToolKits.SectionGenerator.Core.Sections;

/// <summary>
/// 剖面组合器 - 核心算法层
/// 接收剖切线和构件列表，计算并返回局部剖面坐标中的 SectionGeometryData。
/// 模板增强启用时优先走复合墙/复合楼板路径，未启用则保持主线 legacy 行为。
/// </summary>
public sealed class SectionComposer
{
    private readonly FloorVerticalProfileBuilder _verticalProfileBuilder;
    private readonly SightLineComposer _sightLineComposer;

    /// <summary>
    /// 仅用于测试和兼容回退；生产链请通过 DI 注入共享的 FloorVerticalProfileBuilder。
    /// </summary>
    public SectionComposer()
        : this(new FloorVerticalProfileBuilder(), new SightLineComposer())
    {
    }

    public SectionComposer(FloorVerticalProfileBuilder verticalProfileBuilder)
        : this(verticalProfileBuilder, new SightLineComposer())
    {
    }

    public SectionComposer(FloorVerticalProfileBuilder verticalProfileBuilder, SightLineComposer sightLineComposer)
    {
        _verticalProfileBuilder = verticalProfileBuilder;
        _sightLineComposer = sightLineComposer;
    }

    public SectionGeometryData Generate(
        Line3D sectionLine,
        Vector3D viewDirection,
        IEnumerable<BuildingElement> elements,
        FloorConfig floorConfig,
        double baseElevation = 0)
        => Generate(
            sectionLine,
            viewDirection,
            new SectionFloorRecognitionData
            {
                Floor = floorConfig,
                CutElements = elements.ToList()
            },
            baseElevation);

    public SectionGeometryData Generate(
        Line3D sectionLine,
        Vector3D viewDirection,
        SectionFloorRecognitionData recognitionData,
        double baseElevation = 0)
    {
        var projector = new SectionCoordinateProjector(sectionLine);
        var floorConfig = recognitionData.Floor;
        var verticalProfile = _verticalProfileBuilder.Build(floorConfig, projector.SectionLength, baseElevation);
        var context = new SectionGeometryContext
        {
            SectionLine = sectionLine,
            ViewDirection = viewDirection,
            Projector = projector,
            VerticalProfile = verticalProfile
        };

        var elementList = recognitionData.CutElements.ToList();
        var slabElements = elementList
            .Where(static element => element is Slab or CompositeSlabElement)
            .ToList();
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

        foreach (var slab in slabElements)
        {
            ElementSectionData? data = slab switch
            {
                CompositeSlabElement compositeSlab => BuildCompositeSlabElementData(compositeSlab, context, wallIntervals),
                Slab legacySlab => BuildLegacySlabElementData(legacySlab, context, wallIntervals),
                _ => null
            };

            if (data == null || data.CutLineSegments.Count == 0)
            {
                continue;
            }

            elementDataList.Add(data);
        }

        elementDataList.AddRange(_sightLineComposer.Compose(recognitionData.SightLineCandidates, baseElevation));

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
            CompositeWallElement compositeWall => BuildCompositeWallElementData(compositeWall, context),
            Wall wall => BuildLegacyWallElementData(wall, context),
            Column column => BuildColumnElementData(column, context),
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

        var cutLineSegments = new List<SectionLineSegment>();
        var hatchRegions = new List<SectionHatchRegion>();
        var layerIndex = 0;
        for (int i = 0; i + 3 < worldLines.Count && layerIndex < wall.LayerSections.Count; i += 4, layerIndex++)
        {
            var layer = wall.LayerSections[layerIndex];
            var projected = worldLines.Skip(i).Take(4)
                .Select(context.Projector.ProjectLine)
                .Where(line => !IsDegenerate(line))
                .ToList();
            if (projected.Count < 4)
            {
                continue;
            }

            var role = layer.IsCore ? SectionLineRole.Structural : SectionLineRole.Finish;
            cutLineSegments.AddRange(projected.Select(line => new SectionLineSegment
            {
                Line = line,
                Role = role
            }));

            hatchRegions.Add(new SectionHatchRegion
            {
                Category = SectionHatchCategory.Wall,
                Boundary = new[]
                {
                    projected[0].Start,
                    projected[1].Start,
                    projected[1].End,
                    projected[0].End
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

        var topSegments = new[] { topLine }
            .Where(line => !IsDegenerate(line))
            .ToList();
        var bottomSegments = new[] { bottomLine }
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

        var hatchRegions = BuildSlabHatchRegions(topSegments, bottomSegments);

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

        var cutLineSegments = new List<SectionLineSegment>();
        var hatchRegions = new List<SectionHatchRegion>();

        var coreLayer = slab.LayerSections.FirstOrDefault(layer => layer.IsCore && layer.VisibleInSection);
        if (coreLayer != null)
        {
            var coreTop = context.Projector.ProjectLine(new Line3D(
                new Point3D(left.X, left.Y, slab.CoreArea.CoreTopElevation + coreLayer.TopOffset),
                new Point3D(right.X, right.Y, slab.CoreArea.CoreTopElevation + coreLayer.TopOffset)));
            var coreBottom = context.Projector.ProjectLine(new Line3D(
                new Point3D(left.X, left.Y, slab.CoreArea.CoreTopElevation + coreLayer.BottomOffset),
                new Point3D(right.X, right.Y, slab.CoreArea.CoreTopElevation + coreLayer.BottomOffset)));

            if (!IsDegenerate(coreTop))
            {
                cutLineSegments.Add(new SectionLineSegment { Line = coreTop, Role = SectionLineRole.Structural });
            }

            if (!IsDegenerate(coreBottom))
            {
                cutLineSegments.Add(new SectionLineSegment { Line = coreBottom, Role = SectionLineRole.Structural });
            }

            hatchRegions.AddRange(BuildSlabHatchRegions(
                new[] { coreTop }.Where(line => !IsDegenerate(line)).ToList(),
                new[] { coreBottom }.Where(line => !IsDegenerate(line)).ToList()));
        }

        var topFinish = slab.LayerSections
            .Where(layer => !layer.IsCore && layer.VisibleInSection && layer.Side == SlabLayerSide.Top)
            .OrderByDescending(layer => Math.Max(layer.TopOffset, layer.BottomOffset))
            .FirstOrDefault();
        if (topFinish != null)
        {
            var finishTop = context.Projector.ProjectLine(new Line3D(
                new Point3D(left.X, left.Y, slab.CoreArea.CoreTopElevation + Math.Max(topFinish.TopOffset, topFinish.BottomOffset)),
                new Point3D(right.X, right.Y, slab.CoreArea.CoreTopElevation + Math.Max(topFinish.TopOffset, topFinish.BottomOffset))));

            cutLineSegments.AddRange(ClipAtWallFaces(finishTop, wallIntervals)
                .Where(line => !IsDegenerate(line))
                .Select(line => new SectionLineSegment
                {
                    Line = line,
                    Role = SectionLineRole.Finish
                }));
        }

        var bottomFinish = slab.LayerSections
            .Where(layer => !layer.IsCore && layer.VisibleInSection && layer.Side == SlabLayerSide.Bottom)
            .OrderBy(layer => Math.Min(layer.TopOffset, layer.BottomOffset))
            .FirstOrDefault();
        if (bottomFinish != null)
        {
            var finishBottom = context.Projector.ProjectLine(new Line3D(
                new Point3D(left.X, left.Y, slab.CoreArea.CoreTopElevation + Math.Min(bottomFinish.TopOffset, bottomFinish.BottomOffset)),
                new Point3D(right.X, right.Y, slab.CoreArea.CoreTopElevation + Math.Min(bottomFinish.TopOffset, bottomFinish.BottomOffset))));

            cutLineSegments.AddRange(ClipAtWallFaces(finishBottom, wallIntervals)
                .Where(line => !IsDegenerate(line))
                .Select(line => new SectionLineSegment
                {
                    Line = line,
                    Role = SectionLineRole.Finish
                }));
        }

        if (cutLineSegments.Count == 0)
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
            CutLines = cutLineSegments.Select(segment => segment.Line).ToList(),
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

    private static IReadOnlyList<SectionHatchRegion> BuildSlabHatchRegions(
        IReadOnlyList<Line3D> topSegments,
        IReadOnlyList<Line3D> bottomSegments)
    {
        var orderedTop = topSegments
            .OrderBy(line => Math.Min(line.Start.X, line.End.X))
            .ToList();
        var orderedBottom = bottomSegments
            .OrderBy(line => Math.Min(line.Start.X, line.End.X))
            .ToList();

        var regions = new List<SectionHatchRegion>();
        var regionCount = Math.Min(orderedTop.Count, orderedBottom.Count);
        for (int i = 0; i < regionCount; i++)
        {
            var top = orderedTop[i];
            var bottom = orderedBottom[i];
            if (IsDegenerate(top) || IsDegenerate(bottom))
            {
                continue;
            }

            regions.Add(new SectionHatchRegion
            {
                Category = SectionHatchCategory.Slab,
                Boundary = new[]
                {
                    new Point3D(bottom.Start.X, bottom.Start.Y, 0),
                    new Point3D(bottom.End.X, bottom.End.Y, 0),
                    new Point3D(top.End.X, top.End.Y, 0),
                    new Point3D(top.Start.X, top.Start.Y, 0)
                }
            });
        }

        return regions;
    }
}
