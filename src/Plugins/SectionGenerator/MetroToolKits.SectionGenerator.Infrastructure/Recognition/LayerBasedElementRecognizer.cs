using Autodesk.AutoCAD.DatabaseServices;
using Microsoft.Extensions.Logging;
using MetroToolKits.Foundation.Core.Geometry;
using MetroToolKits.SectionGenerator.App.Abstractions;
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

    public IReadOnlyList<BuildingElement> RecognizeElements(Line3D sectionLine, double viewDepth)
    {
        var doc = Application.DocumentManager.MdiActiveDocument;
        if (doc == null) return Array.Empty<BuildingElement>();

        var db = doc.Database;
        var result = new List<BuildingElement>();
        var viewDirection = ComputeViewDirection(sectionLine);

        using var tr = db.TransactionManager.StartTransaction();
        var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
        var btr = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);

        int scannedCount = 0;
        foreach (var objId in btr)
        {
            scannedCount++;
            if (tr.GetObject(objId, OpenMode.ForRead) is not Entity entity) continue;

            var elementType = GetElementTypeFromLayer(entity.Layer);
            if (elementType == null) continue;

            var element = ConvertToElement(entity, elementType);
            if (element == null) continue;
            if (!IntersectsSection(element, sectionLine, viewDirection)) continue;

            result.Add(element);
            _logger.LogTrace("识别构件: Handle={Handle}, Type={Type}, Layer={Layer}",
                entity.Handle, elementType, entity.Layer);
        }

        tr.Commit();
        _logger.LogDebug("扫描 {ScannedCount} 个实体，在剖切线附近识别到 {ElementCount} 个构件，视图深度 {ViewDepth}",
            scannedCount, result.Count, viewDepth);
        return result;
    }

    private static string? GetElementTypeFromLayer(string layerName)
    {
        foreach (var kv in LayerTypeMap)
            if (layerName.StartsWith(kv.Value, StringComparison.OrdinalIgnoreCase))
                return kv.Key;
        return null;
    }

    private static BuildingElement? ConvertToElement(Entity entity, string elementType)
    {
        try
        {
            return elementType switch
            {
                "Wall"   => ConvertToWall(entity),
                "Column" => ConvertToColumn(entity),
                "Slab"   => ConvertToSlab(entity),
                _        => null
            };
        }
        catch { return null; }
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
}
