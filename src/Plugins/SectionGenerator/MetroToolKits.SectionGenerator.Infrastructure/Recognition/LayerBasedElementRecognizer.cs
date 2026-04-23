using Autodesk.AutoCAD.DatabaseServices;
using Microsoft.Extensions.Logging;
using MetroToolKits.Foundation.Building.Elements;
using MetroToolKits.Foundation.Building.Types;
using MetroToolKits.Foundation.Core.Diagnostics;
using MetroToolKits.Foundation.Core.Geometry;
using MetroToolKits.SectionGenerator.App.Abstractions;
using MetroToolKits.SectionGenerator.App.Diagnostics;
using MetroToolKits.SectionGenerator.Core.Sections;
using MetroToolKits.SectionGenerator.Infrastructure.Services;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;
using BuildingColumn = MetroToolKits.Foundation.Building.Elements.Column;
using BuildingWall = MetroToolKits.Foundation.Building.Elements.Wall;
using BuildingSlab = MetroToolKits.Foundation.Building.Elements.Slab;
using BuildingElement = MetroToolKits.Foundation.Building.Elements.BuildingElement;

namespace MetroToolKits.SectionGenerator.Infrastructure.Recognition;

/// <summary>
/// 基于图层映射的构件识别器。
/// 增强模式支持模板驱动装配，但未绑定模板时仍沿用主线稳定识别。
/// </summary>
public sealed class LayerBasedElementRecognizer : IElementRecognizer
{
    private readonly ILogger<LayerBasedElementRecognizer> _logger;
    private readonly ElementConversionBackupService _backupService;
    private readonly IWallAssemblyTemplateCatalog _wallTemplateCatalog;
    private readonly IWallAssemblyBuilder _wallAssemblyBuilder;
    private readonly ISlabAssemblyTemplateCatalog _slabTemplateCatalog;
    private readonly ISlabAssemblyBuilder _slabAssemblyBuilder;

    private static readonly Dictionary<string, string> LayerTypeMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Wall"] = "MK_结构墙",
        ["Column"] = "MK_结构柱",
        ["Slab"] = "MK_楼板"
    };

    public LayerBasedElementRecognizer(
        ILogger<LayerBasedElementRecognizer> logger,
        ElementConversionBackupService backupService,
        IWallAssemblyTemplateCatalog wallTemplateCatalog,
        IWallAssemblyBuilder wallAssemblyBuilder,
        ISlabAssemblyTemplateCatalog slabTemplateCatalog,
        ISlabAssemblyBuilder slabAssemblyBuilder)
    {
        _logger = logger;
        _backupService = backupService;
        _wallTemplateCatalog = wallTemplateCatalog;
        _wallAssemblyBuilder = wallAssemblyBuilder;
        _slabTemplateCatalog = slabTemplateCatalog;
        _slabAssemblyBuilder = slabAssemblyBuilder;
    }

    public ElementRecognitionResult RecognizeElements(Line3D sectionLine, double viewDepth, ScopeBounds2D? scopeBounds = null)
    {
        var doc = Application.DocumentManager.MdiActiveDocument;
        if (doc == null)
        {
            throw CreateInfrastructureException("无活动文档，无法执行构件识别。");
        }

        try
        {
            var elements = new List<BuildingElement>();
            var diagnostics = new List<OperationDiagnostic>();
            var viewDirection = ComputeViewDirection(sectionLine);
            var matchedLayerCount = 0;
            var intersectingElementCount = 0;
            var wallCandidates = new Dictionary<(string Layer, string TemplateId), List<WallCandidate>>();

            _logger.LogDebug("当前识别器暂未使用 viewDepth 过滤，收到视图深度 {ViewDepth}", viewDepth);

            using var tr = doc.Database.TransactionManager.StartTransaction();
            var bt = (BlockTable)tr.GetObject(doc.Database.BlockTableId, OpenMode.ForRead);
            var modelSpace = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);

            var scannedCount = 0;
            foreach (var objectId in modelSpace)
            {
                scannedCount++;
                if (tr.GetObject(objectId, OpenMode.ForRead) is not Entity entity)
                {
                    continue;
                }

                var elementType = ResolveElementType(tr, entity);
                if (elementType == null)
                {
                    continue;
                }

                matchedLayerCount++;
                var templateId = _backupService.GetTemplateId(tr, entity);

                if (string.Equals(elementType, "Wall", StringComparison.OrdinalIgnoreCase) &&
                    !string.IsNullOrWhiteSpace(templateId))
                {
                    var template = _wallTemplateCatalog.GetById(templateId);
                    if (template != null)
                    {
                        var candidate = ConvertToWallCandidate(entity, template, diagnostics);
                        if (candidate != null)
                        {
                            var key = (entity.Layer, template.TemplateId);
                            if (!wallCandidates.TryGetValue(key, out var candidates))
                            {
                                candidates = new List<WallCandidate>();
                                wallCandidates[key] = candidates;
                            }

                            candidates.Add(candidate);
                            continue;
                        }
                    }
                }

                var element = ConvertToElement(entity, elementType, templateId, diagnostics);
                if (element == null)
                {
                    continue;
                }

                if (!TryAddRecognizedElement(
                        element,
                        sectionLine,
                        viewDirection,
                        scopeBounds,
                        entity.Layer,
                        diagnostics,
                        elements,
                        ref intersectingElementCount))
                {
                    continue;
                }
            }

            foreach (var ((layerName, templateId), candidates) in wallCandidates)
            {
                var template = _wallTemplateCatalog.GetById(templateId);
                if (template == null)
                {
                    foreach (var candidate in candidates)
                    {
                        var legacyWall = CreateLegacyWallFromCandidate(candidate, templateId);
                        if (!TryAddRecognizedElement(
                                legacyWall,
                                sectionLine,
                                viewDirection,
                                scopeBounds,
                                layerName,
                                diagnostics,
                                elements,
                                ref intersectingElementCount))
                        {
                            continue;
                        }
                    }

                    continue;
                }

                if (template.CoreRule.RecognitionMode == WallCoreRecognitionMode.Centerline)
                {
                    foreach (var candidate in candidates)
                    {
                        var core = BuildCenterlineCoreWall(candidate, template);
                        var wall = _wallAssemblyBuilder.Build(core, template);
                        if (!TryAddRecognizedElement(
                                wall,
                                sectionLine,
                                viewDirection,
                                scopeBounds,
                                layerName,
                                diagnostics,
                                elements,
                                ref intersectingElementCount))
                        {
                            continue;
                        }
                    }

                    continue;
                }

                ProcessBoundaryPairWalls(
                    candidates,
                    template,
                    sectionLine,
                    viewDirection,
                    scopeBounds,
                    diagnostics,
                    elements,
                    ref intersectingElementCount);
            }

            tr.Commit();

            return new ElementRecognitionResult
            {
                Elements = elements,
                Diagnostics = diagnostics,
                ScannedEntityCount = scannedCount,
                MatchedLayerCount = matchedLayerCount,
                IntersectingElementCount = intersectingElementCount
            };
        }
        catch (InfrastructureException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "构件识别失败");
            throw CreateInfrastructureException($"构件识别过程中发生异常: {ex.Message}", ex);
        }
    }

    private string? ResolveElementType(Transaction tr, Entity entity)
    {
        var convertedType = _backupService.GetConvertedType(tr, entity);
        if (!string.IsNullOrWhiteSpace(convertedType))
        {
            return convertedType;
        }

        return GetElementTypeFromLayer(entity.Layer);
    }

    private static string? GetElementTypeFromLayer(string layerName)
    {
        foreach (var mapping in LayerTypeMap)
        {
            if (layerName.StartsWith(mapping.Value, StringComparison.OrdinalIgnoreCase))
            {
                return mapping.Key;
            }
        }

        return null;
    }

    private BuildingElement? ConvertToElement(
        Entity entity,
        string elementType,
        string? templateId,
        ICollection<OperationDiagnostic> diagnostics)
    {
        try
        {
            BuildingElement? converted = elementType switch
            {
                "Wall" => ConvertToWall(entity),
                "Column" => ConvertToColumn(entity),
                "Slab" => ConvertToSlab(entity, templateId),
                _ => null
            };

            if (converted != null)
            {
                return converted;
            }

            diagnostics.Add(SectionGenerationDiagnosticFactory.UnsupportedEntityType(
                nameof(LayerBasedElementRecognizer),
                entity.Handle.ToString(),
                entity.Layer,
                entity.GetType().Name,
                GetExpectedEntityTypeName(elementType)));

            return null;
        }
        catch (Exception ex)
        {
            diagnostics.Add(SectionGenerationDiagnosticFactory.ConversionFailed(
                nameof(LayerBasedElementRecognizer),
                entity.Handle.ToString(),
                elementType,
                ex.Message));
            return null;
        }
    }

    private bool TryAddRecognizedElement(
        BuildingElement element,
        Line3D sectionLine,
        Vector3D viewDirection,
        ScopeBounds2D? scopeBounds,
        string layerName,
        ICollection<OperationDiagnostic> diagnostics,
        ICollection<BuildingElement> recognizedElements,
        ref int intersectingElementCount)
    {
        bool intersectsSection;
        try
        {
            intersectsSection = IntersectsSection(element, sectionLine, viewDirection);
        }
        catch (Exception ex)
        {
            diagnostics.Add(SectionGenerationDiagnosticFactory.ConversionFailed(
                nameof(LayerBasedElementRecognizer),
                element.SourceHandle,
                element.ElementType,
                $"剖切求交失败: {ex.Message}"));
            return false;
        }

        if (!intersectsSection)
        {
            diagnostics.Add(SectionGenerationDiagnosticFactory.NoIntersectingElements(
                nameof(LayerBasedElementRecognizer),
                element.SourceHandle,
                element.ElementType,
                layerName));
            return false;
        }

        if (scopeBounds.HasValue && !IntersectsScope(element, scopeBounds.Value))
        {
            return false;
        }

        intersectingElementCount++;
        recognizedElements.Add(element);
        _logger.LogTrace(
            "识别构件: Handle={Handle}, Type={Type}, Layer={Layer}",
            element.SourceHandle,
            element.ElementType,
            layerName);
        return true;
    }

    private static bool IntersectsSection(BuildingElement element, Line3D sectionLine, Vector3D viewDirection)
    {
        var context = new SectionGeometryContext
        {
            SectionLine = sectionLine,
            ViewDirection = viewDirection,
            Projector = new SectionCoordinateProjector(sectionLine)
        };

        return element.GetSectionGeometry(context).Any();
    }

    private static bool IntersectsScope(BuildingElement element, ScopeBounds2D scopeBounds)
    {
        var elementBounds = ScopeBounds2D.FromPolygon(element.GetBoundingBox());
        return elementBounds.HasValue && elementBounds.Value.Intersects(scopeBounds);
    }

    private void ProcessBoundaryPairWalls(
        IReadOnlyList<WallCandidate> candidates,
        WallAssemblyTemplate template,
        Line3D sectionLine,
        Vector3D viewDirection,
        ScopeBounds2D? scopeBounds,
        ICollection<OperationDiagnostic> diagnostics,
        ICollection<BuildingElement> recognizedElements,
        ref int intersectingElementCount)
    {
        var intersections = candidates
            .Select(candidate => new WallIntersection(
                candidate,
                IntersectSectionLine(sectionLine, candidate.Segment)))
            .Where(item => item.Intersection.HasValue)
            .Select(item => item with
            {
                Chainage = SectionCoordinateProjector.GetChainage(sectionLine, item.Intersection!.Value)
            })
            .OrderBy(item => item.Chainage)
            .ToList();

        if (intersections.Count == 0)
        {
            foreach (var candidate in candidates)
            {
                diagnostics.Add(SectionGenerationDiagnosticFactory.NoIntersectingElements(
                    nameof(LayerBasedElementRecognizer),
                    candidate.SourceHandle,
                    "Wall",
                    candidate.LayerName));
            }

            return;
        }

        var pairings = BuildPairings(intersections, diagnostics, template);
        foreach (var (left, right) in pairings)
        {
            if (!TryBuildCoreFromPair(left, right, template, out var core))
            {
                continue;
            }

            var wall = _wallAssemblyBuilder.Build(core!, template);
            if (!TryAddRecognizedElement(
                    wall,
                    sectionLine,
                    viewDirection,
                    scopeBounds,
                    left.Candidate.LayerName,
                    diagnostics,
                    recognizedElements,
                    ref intersectingElementCount))
            {
                continue;
            }
        }
    }

    private static BuildingWall? ConvertToWall(Entity entity)
    {
        return TryGetWallSegment(entity, out var segment)
            ? new BuildingWall
            {
                SourceHandle = entity.Handle.ToString(),
                SourceHandles = new List<string> { entity.Handle.ToString() },
                SourceLayer = entity.Layer,
                StartPoint = segment.Start,
                EndPoint = segment.End,
                Height = 3000,
                Thickness = 200,
                BaseElevation = 0
            }
            : null;
    }

    private static WallCandidate? ConvertToWallCandidate(
        Entity entity,
        WallAssemblyTemplate template,
        ICollection<OperationDiagnostic> diagnostics)
    {
        if (!TryGetWallSegment(entity, out var segment))
        {
            diagnostics.Add(SectionGenerationDiagnosticFactory.UnsupportedEntityType(
                nameof(LayerBasedElementRecognizer),
                entity.Handle.ToString(),
                entity.Layer,
                entity.GetType().Name,
                $"{nameof(Line)} / Open {nameof(Polyline)}(2 vertices)"));
            return null;
        }

        return new WallCandidate(
            entity.Handle.ToString(),
            entity.Layer,
            segment,
            template.TemplateId,
            template.CoreRule.RecognitionMode);
    }

    private static BuildingWall CreateLegacyWallFromCandidate(WallCandidate candidate, string? templateId)
    {
        return new BuildingWall
        {
            SourceHandle = candidate.SourceHandle,
            SourceHandles = new List<string> { candidate.SourceHandle },
            SourceLayer = candidate.LayerName,
            StartPoint = candidate.Segment.Start,
            EndPoint = candidate.Segment.End,
            Height = 3000,
            Thickness = 200,
            BaseElevation = 0,
            Name = string.IsNullOrWhiteSpace(templateId) ? null : $"legacy:{templateId}"
        };
    }

    private static BuildingColumn? ConvertToColumn(Entity entity)
    {
        if (entity is Circle circle)
        {
            return new BuildingColumn
            {
                SourceHandle = entity.Handle.ToString(),
                SourceHandles = new List<string> { entity.Handle.ToString() },
                SourceLayer = entity.Layer,
                CenterPoint = new Point3D(circle.Center.X, circle.Center.Y, circle.Center.Z),
                Width = circle.Radius * 2,
                Depth = circle.Radius * 2,
                Height = 3000,
                BaseElevation = 0
            };
        }

        if (entity is Polyline polyline &&
            TryCreateRectangularColumn(polyline, out var centerPoint, out var width, out var depth, out var rotation))
        {
            return new BuildingColumn
            {
                SourceHandle = entity.Handle.ToString(),
                SourceHandles = new List<string> { entity.Handle.ToString() },
                SourceLayer = entity.Layer,
                CenterPoint = centerPoint,
                Width = width,
                Depth = depth,
                Rotation = rotation,
                Height = 3000,
                BaseElevation = 0
            };
        }

        return null;
    }

    private BuildingElement? ConvertToSlab(Entity entity, string? templateId)
    {
        if (entity is not Polyline polyline)
        {
            return null;
        }

        var outline = new List<Point3D>();
        for (var i = 0; i < polyline.NumberOfVertices; i++)
        {
            var point = polyline.GetPoint3dAt(i);
            outline.Add(new Point3D(point.X, point.Y, point.Z));
        }

        if (!string.IsNullOrWhiteSpace(templateId))
        {
            var template = _slabTemplateCatalog.GetById(templateId);
            if (template != null)
            {
                return _slabAssemblyBuilder.Build(
                    new CoreSlabArea
                    {
                        TemplateId = template.TemplateId,
                        Outline = outline,
                        CoreTopElevation = 0,
                        SourceLayer = entity.Layer,
                        SourceHandles = new List<string> { entity.Handle.ToString() }
                    },
                    template);
            }
        }

        return new BuildingSlab
        {
            SourceHandle = entity.Handle.ToString(),
            SourceHandles = new List<string> { entity.Handle.ToString() },
            SourceLayer = entity.Layer,
            Outline = outline,
            Thickness = 200,
            TopElevation = 0
        };
    }

    private static bool TryGetWallSegment(Entity entity, out Line3D segment)
    {
        segment = default;
        if (entity is Line line)
        {
            segment = new Line3D(
                new Point3D(line.StartPoint.X, line.StartPoint.Y, line.StartPoint.Z),
                new Point3D(line.EndPoint.X, line.EndPoint.Y, line.EndPoint.Z));
            return true;
        }

        if (entity is Polyline polyline && !polyline.Closed && polyline.NumberOfVertices == 2)
        {
            var start = polyline.GetPoint3dAt(0);
            var end = polyline.GetPoint3dAt(1);
            segment = new Line3D(
                new Point3D(start.X, start.Y, start.Z),
                new Point3D(end.X, end.Y, end.Z));
            return true;
        }

        return false;
    }

    private static bool TryCreateRectangularColumn(
        Polyline polyline,
        out Point3D centerPoint,
        out double width,
        out double depth,
        out double rotation)
    {
        centerPoint = default;
        width = 0;
        depth = 0;
        rotation = 0;

        if (!polyline.Closed || polyline.NumberOfVertices != 4)
        {
            return false;
        }

        var points = Enumerable.Range(0, 4)
            .Select(polyline.GetPoint3dAt)
            .Select(point => new Point3D(point.X, point.Y, point.Z))
            .ToList();

        var edge1 = new Vector3D(points[1].X - points[0].X, points[1].Y - points[0].Y, 0);
        var edge2 = new Vector3D(points[2].X - points[1].X, points[2].Y - points[1].Y, 0);
        var edge3 = new Vector3D(points[3].X - points[2].X, points[3].Y - points[2].Y, 0);
        var edge4 = new Vector3D(points[0].X - points[3].X, points[0].Y - points[3].Y, 0);

        var widthCandidate = edge1.Length;
        var depthCandidate = edge2.Length;
        if (widthCandidate <= 1e-6 || depthCandidate <= 1e-6)
        {
            return false;
        }

        var normalized1 = edge1.Normalized;
        var normalized2 = edge2.Normalized;
        var normalized3 = edge3.Normalized;
        var normalized4 = edge4.Normalized;

        if (Math.Abs(Vector3D.Dot(normalized1, normalized2)) > 1e-3)
        {
            return false;
        }

        if (Math.Abs(Math.Abs(Vector3D.Dot(normalized1, normalized3)) - 1) > 1e-3 ||
            Math.Abs(Math.Abs(Vector3D.Dot(normalized2, normalized4)) - 1) > 1e-3)
        {
            return false;
        }

        centerPoint = new Point3D(points.Average(point => point.X), points.Average(point => point.Y), points.Average(point => point.Z));
        width = widthCandidate;
        depth = depthCandidate;
        rotation = Math.Atan2(edge1.Y, edge1.X);
        return true;
    }

    private static Vector3D ComputeViewDirection(Line3D sectionLine)
    {
        var direction = sectionLine.Direction.Normalized;
        return new Vector3D(-direction.Y, direction.X, 0);
    }

    private static IReadOnlyList<(WallIntersection Left, WallIntersection Right)> BuildPairings(
        IReadOnlyList<WallIntersection> intersections,
        ICollection<OperationDiagnostic> diagnostics,
        WallAssemblyTemplate template)
    {
        var result = new List<(WallIntersection Left, WallIntersection Right)>();
        if (intersections.Count == 3)
        {
            result.Add((intersections[0], intersections[1]));
            result.Add((intersections[1], intersections[2]));
            return result;
        }

        if (intersections.Count % 2 != 0)
        {
            diagnostics.Add(SectionGenerationDiagnosticFactory.AmbiguousWallPairing(
                nameof(LayerBasedElementRecognizer),
                intersections[0].Candidate.LayerName,
                template.TemplateId,
                intersections.Count));
            return result;
        }

        for (int i = 0; i < intersections.Count; i += 2)
        {
            result.Add((intersections[i], intersections[i + 1]));
        }

        return result;
    }

    private static bool TryBuildCoreFromPair(
        WallIntersection left,
        WallIntersection right,
        WallAssemblyTemplate template,
        out CoreWallSegment? coreSegment)
    {
        coreSegment = null;

        if (!TryAlignSegments(left.Candidate.Segment, right.Candidate.Segment, out var alignedLeft, out var alignedRight))
        {
            return false;
        }

        var leftDirection = alignedLeft.Direction.Normalized;
        if (leftDirection.Length <= 1e-9)
        {
            return false;
        }

        var normal = new Vector3D(-leftDirection.Y, leftDirection.X, 0).Normalized;
        var delta = new Vector3D(
            alignedRight.Start.X - alignedLeft.Start.X,
            alignedRight.Start.Y - alignedLeft.Start.Y,
            0);
        var thickness = Math.Abs(Vector3D.Dot(delta, normal));
        if (thickness <= 1e-6)
        {
            return false;
        }

        coreSegment = new CoreWallSegment
        {
            StartPoint = MidPoint(alignedLeft.Start, alignedRight.Start),
            EndPoint = MidPoint(alignedLeft.End, alignedRight.End),
            Thickness = thickness,
            Height = 3000,
            BaseElevation = 0,
            TemplateId = template.TemplateId,
            SourceLayer = left.Candidate.LayerName,
            SourceHandles = new List<string>
            {
                left.Candidate.SourceHandle,
                right.Candidate.SourceHandle
            }
        };
        return true;
    }

    private static CoreWallSegment BuildCenterlineCoreWall(WallCandidate candidate, WallAssemblyTemplate template)
    {
        return new CoreWallSegment
        {
            StartPoint = candidate.Segment.Start,
            EndPoint = candidate.Segment.End,
            Thickness = template.CoreRule.Thickness,
            Height = 3000,
            BaseElevation = 0,
            TemplateId = template.TemplateId,
            SourceLayer = candidate.LayerName,
            SourceHandles = new List<string> { candidate.SourceHandle }
        };
    }

    private static Point3D? IntersectSectionLine(Line3D sectionLine, Line3D segment)
    {
        var x1 = sectionLine.Start.X; var y1 = sectionLine.Start.Y;
        var x2 = sectionLine.End.X; var y2 = sectionLine.End.Y;
        var x3 = segment.Start.X; var y3 = segment.Start.Y;
        var x4 = segment.End.X; var y4 = segment.End.Y;

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
            sectionLine.Start.Z + t * (sectionLine.End.Z - sectionLine.Start.Z));
    }

    private static bool TryAlignSegments(Line3D left, Line3D right, out Line3D alignedLeft, out Line3D alignedRight)
    {
        alignedLeft = left;
        alignedRight = right;

        var sameDirection = left.Start.DistanceTo(right.Start) + left.End.DistanceTo(right.End);
        var reversedDirection = left.Start.DistanceTo(right.End) + left.End.DistanceTo(right.Start);
        if (reversedDirection < sameDirection)
        {
            alignedRight = new Line3D(right.End, right.Start);
        }

        var leftDirection = alignedLeft.Direction.Normalized;
        var rightDirection = alignedRight.Direction.Normalized;
        return leftDirection.Length > 1e-9 &&
               rightDirection.Length > 1e-9 &&
               Math.Abs(Vector3D.Dot(leftDirection, rightDirection)) >= 0.95;
    }

    private static Point3D MidPoint(Point3D left, Point3D right)
        => new((left.X + right.X) / 2.0, (left.Y + right.Y) / 2.0, (left.Z + right.Z) / 2.0);

    private static string GetExpectedEntityTypeName(string elementType)
    {
        return elementType switch
        {
            "Wall" => $"{nameof(Line)} / Open {nameof(Polyline)}(2 vertices)",
            "Column" => $"{nameof(Circle)} / Closed {nameof(Polyline)}(4 vertices)",
            "Slab" => nameof(Polyline),
            _ => "Unknown"
        };
    }

    private static InfrastructureException CreateInfrastructureException(string technicalMessage, Exception? innerException = null)
    {
        return new InfrastructureException(new OperationFailure
        {
            Code = SectionGenerationErrorCodes.Unexpected,
            Category = FailureCategory.Infrastructure,
            Stage = PipelineStage.ElementRecognition,
            Module = nameof(LayerBasedElementRecognizer),
            UserMessage = "构件识别失败，请检查当前图纸环境",
            TechnicalMessage = technicalMessage,
            InnerException = innerException
        });
    }

    private sealed record WallCandidate(
        string SourceHandle,
        string LayerName,
        Line3D Segment,
        string TemplateId,
        WallCoreRecognitionMode RecognitionMode);

    private sealed record WallIntersection(
        WallCandidate Candidate,
        Point3D? Intersection)
    {
        public double Chainage { get; init; }
    }
}
