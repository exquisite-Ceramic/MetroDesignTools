using Autodesk.AutoCAD.DatabaseServices;
using Microsoft.Extensions.Logging;
using MetroToolKits.Foundation.Core.Diagnostics;
using MetroToolKits.Foundation.Core.Geometry;
using MetroToolKits.SectionGenerator.App.Abstractions;
using MetroToolKits.SectionGenerator.App.Diagnostics;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;
using BuildingColumn = MetroToolKits.Foundation.Building.Elements.Column;
using BuildingWall   = MetroToolKits.Foundation.Building.Elements.Wall;
using BuildingSlab   = MetroToolKits.Foundation.Building.Elements.Slab;
using BuildingElement = MetroToolKits.Foundation.Building.Elements.BuildingElement;

namespace MetroToolKits.SectionGenerator.Infrastructure.Recognition;

/// <summary>
/// 基于图层映射的构件识别器
/// </summary>
public sealed class LayerBasedElementRecognizer : IElementRecognizer
{
    private readonly ILogger<LayerBasedElementRecognizer> _logger;

    private static readonly Dictionary<string, string> LayerTypeMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Wall"]   = "MK_结构墙",
        ["Column"] = "MK_结构柱",
        ["Slab"]   = "MK_楼板",
    };

    public LayerBasedElementRecognizer(ILogger<LayerBasedElementRecognizer> logger)
    {
        _logger = logger;
    }

    public ElementRecognitionResult RecognizeElements(Line3D sectionLine, double viewDepth)
    {
        var doc = Application.DocumentManager.MdiActiveDocument;
        if (doc == null)
            throw CreateInfrastructureException("无活动文档，无法执行构件识别。");

        try
        {
            var db = doc.Database;
            var elements = new List<BuildingElement>();
            var diagnostics = new List<OperationDiagnostic>();
            var viewDirection = ComputeViewDirection(sectionLine);
            var matchedLayerCount = 0;
            var intersectingElementCount = 0;

            _logger.LogDebug("当前识别器暂未使用 viewDepth 过滤，收到视图深度 {ViewDepth}", viewDepth);

            using var tr = db.TransactionManager.StartTransaction();
            var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
            var btr = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);

            var scannedCount = 0;
            foreach (var objId in btr)
            {
                scannedCount++;
                if (tr.GetObject(objId, OpenMode.ForRead) is not Entity entity) continue;

                var elementType = GetElementTypeFromLayer(entity.Layer);
                if (elementType == null) continue;

                matchedLayerCount++;
                var element = ConvertToElement(entity, elementType, diagnostics);
                if (element == null) continue;

                bool intersectsSection;
                try
                {
                    intersectsSection = IntersectsSection(element, sectionLine, viewDirection);
                }
                catch (Exception ex)
                {
                    diagnostics.Add(SectionGenerationDiagnosticFactory.ConversionFailed(
                        nameof(LayerBasedElementRecognizer),
                        entity.Handle.ToString(),
                        elementType,
                        $"剖切求交失败: {ex.Message}"));
                    continue;
                }

                if (!intersectsSection)
                {
                    diagnostics.Add(SectionGenerationDiagnosticFactory.NoIntersectingElements(
                        nameof(LayerBasedElementRecognizer),
                        entity.Handle.ToString(),
                        elementType,
                        entity.Layer));
                    continue;
                }

                intersectingElementCount++;
                elements.Add(element);
                _logger.LogTrace("识别构件: Handle={Handle}, Type={Type}, Layer={Layer}",
                    entity.Handle, elementType, entity.Layer);
            }

            tr.Commit();
            _logger.LogDebug(
                "扫描 {ScannedCount} 个实体，在剖切线附近识别到 {ElementCount} 个构件，命中支持图层 {MatchedLayerCount} 个，视图深度 {ViewDepth}",
                scannedCount,
                elements.Count,
                matchedLayerCount,
                viewDepth);

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

    private static string? GetElementTypeFromLayer(string layerName)
    {
        foreach (var kv in LayerTypeMap)
            if (layerName.StartsWith(kv.Value, StringComparison.OrdinalIgnoreCase))
                return kv.Key;
        return null;
    }

    private static BuildingElement? ConvertToElement(
        Entity entity,
        string elementType,
        ICollection<OperationDiagnostic> diagnostics)
    {
        try
        {
            BuildingElement? converted = elementType switch
            {
                "Wall"   => ConvertToWall(entity),
                "Column" => ConvertToColumn(entity),
                "Slab"   => ConvertToSlab(entity),
                _        => null
            };

            if (converted != null)
                return converted;

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

    private static bool IntersectsSection(BuildingElement element, Line3D sectionLine, Vector3D viewDirection)
    {
        return element.GetSectionGeometry(sectionLine, viewDirection).Any();
    }

    private static Vector3D ComputeViewDirection(Line3D sectionLine)
    {
        var direction = sectionLine.Direction.Normalized;
        return new Vector3D(-direction.Y, direction.X, 0);
    }

    private static string GetExpectedEntityTypeName(string elementType)
    {
        return elementType switch
        {
            "Wall" => nameof(Line),
            "Column" => nameof(Circle),
            "Slab" => nameof(Polyline),
            _ => "Unknown"
        };
    }

    private static BuildingWall? ConvertToWall(Entity entity)
    {
        if (entity is not Line line) return null;
        return new BuildingWall
        {
            SourceHandle  = entity.Handle.ToString(),
            SourceLayer   = entity.Layer,
            StartPoint    = new Point3D(line.StartPoint.X, line.StartPoint.Y, line.StartPoint.Z),
            EndPoint      = new Point3D(line.EndPoint.X,   line.EndPoint.Y,   line.EndPoint.Z),
            Height        = 3000,
            Thickness     = 200,
            BaseElevation = 0
        };
    }

    private static BuildingColumn? ConvertToColumn(Entity entity)
    {
        if (entity is not Circle circle) return null;
        return new BuildingColumn
        {
            SourceHandle  = entity.Handle.ToString(),
            SourceLayer   = entity.Layer,
            CenterPoint   = new Point3D(circle.Center.X, circle.Center.Y, circle.Center.Z),
            Width         = circle.Radius * 2,
            Depth         = circle.Radius * 2,
            Height        = 3000,
            BaseElevation = 0
        };
    }

    private static BuildingSlab? ConvertToSlab(Entity entity)
    {
        if (entity is not Polyline pline) return null;
        var outline = new List<Point3D>();
        for (int i = 0; i < pline.NumberOfVertices; i++)
        {
            var pt = pline.GetPoint3dAt(i);
            outline.Add(new Point3D(pt.X, pt.Y, pt.Z));
        }
        return new BuildingSlab
        {
            SourceHandle = entity.Handle.ToString(),
            SourceLayer  = entity.Layer,
            Outline      = outline,
            Thickness    = 200,
            TopElevation = 0
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
}
