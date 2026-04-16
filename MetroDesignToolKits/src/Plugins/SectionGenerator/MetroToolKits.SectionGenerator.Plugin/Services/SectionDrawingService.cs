using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using CoreSectionSettings = SectionGenerator.Core.Sections.SectionSettings;

namespace SectionGenerator.Plugin.Services;

/// <summary>
/// 剖面绘制服务 - 封装所有 CAD 绘制操作
/// </summary>
public class SectionDrawingService
{
    private readonly Database _database;
    private readonly CoreSectionSettings _settings;

    public SectionDrawingService(Database database, CoreSectionSettings settings)
    {
        _database = database;
        _settings = settings;
    }

    public void Draw(Line sectionLine, List<Curve> auxCurves, Point3d insertPt)
    {
        var startPt = sectionLine.StartPoint;
        var endPt = sectionLine.EndPoint;
        var length = startPt.DistanceTo(endPt);
        var direction = (endPt - startPt).GetNormal();

        var intersections = CalculateIntersections(sectionLine, auxCurves, startPt, direction, length);
        DrawSection(insertPt, length, intersections);
    }

    private List<double> CalculateIntersections(Line sectionLine, List<Curve> auxCurves, Point3d startPt, Vector3d direction, double length)
    {
        var result = new List<double>();

        foreach (var curve in auxCurves)
        {
            var pts = new Point3dCollection();
            sectionLine.IntersectWith(curve, Intersect.OnBothOperands, pts, IntPtr.Zero, IntPtr.Zero);

            foreach (Point3d pt in pts)
            {
                var param = (pt - startPt).DotProduct(direction);
                if (param >= 0 && param <= length)
                    result.Add(param);
            }
        }

        result.Sort();
        return result.Distinct().ToList();
    }

    private void DrawSection(Point3d insertPt, double length, List<double> intersections)
    {
        using var tr = _database.TransactionManager.StartTransaction();
        var btr = GetModelSpace(tr);

        var slopeDelta = _settings.HasSlope ? length * _settings.SlopeValue : 0.0;
        var layers = CalculateLayerPositions(length, slopeDelta);

        foreach (var layer in layers)
        {
            DrawLayer(btr, insertPt, layer, tr);
        }

        var maxY = layers.Max(l => System.Math.Max(l.TopStart, l.TopEnd));
        var minY = layers.Min(l => System.Math.Min(l.BottomStart, l.BottomEnd));

        foreach (var param in intersections)
        {
            DrawLine(btr, insertPt, new Point2d(param, minY), new Point2d(param, maxY));
        }

        DrawAnnotations(btr, insertPt, length, maxY, intersections, tr);
        tr.Commit();
    }

    private BlockTableRecord GetModelSpace(Transaction tr)
    {
        var bt = (BlockTable)tr.GetObject(_database.BlockTableId, OpenMode.ForRead);
        return (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
    }

    private List<LayerPosition> CalculateLayerPositions(double length, double slopeDelta)
    {
        var floorHeight = _settings.FloorHeight;
        var topSlabBottomEnd = floorHeight + slopeDelta;

        return new List<LayerPosition>
        {
            new("底板", -_settings.BottomSlabThickness, -_settings.BottomSlabThickness, 0, 0, "AR-CONC"),
            new("顶板", floorHeight, topSlabBottomEnd, floorHeight + _settings.TopSlabThickness, topSlabBottomEnd + _settings.TopSlabThickness, "AR-CONC"),
            new("装修面层", floorHeight + _settings.TopSlabThickness, topSlabBottomEnd + _settings.TopSlabThickness, floorHeight + _settings.TopSlabThickness + _settings.FinishThickness, topSlabBottomEnd + _settings.TopSlabThickness + _settings.FinishThickness, "ANSI31")
        };
    }

    private void DrawLayer(BlockTableRecord btr, Point3d insertPt, LayerPosition layer, Transaction tr)
    {
        DrawHatch(btr, insertPt, layer, tr);
        DrawLine(btr, insertPt, new Point2d(0, layer.BottomStart), new Point2d(0, layer.BottomEnd));
        DrawLine(btr, insertPt, new Point2d(0, layer.TopStart), new Point2d(0, layer.TopEnd));
    }

    private void DrawHatch(BlockTableRecord btr, Point3d insertPt, LayerPosition layer, Transaction tr)
    {
        using var pline = CreateHatchPolyline(insertPt, layer);
        btr.AppendEntity(pline);
        tr.AddNewlyCreatedDBObject(pline, true);

        var hatch = new Hatch();
        btr.AppendEntity(hatch);
        tr.AddNewlyCreatedDBObject(hatch, true);
        hatch.SetHatchPattern(HatchPatternType.PreDefined, layer.HatchPattern);
        hatch.PatternScale = 50;
        hatch.Associative = true;
        hatch.AppendLoop(HatchLoopTypes.External, new ObjectIdCollection { pline.ObjectId });
        hatch.EvaluateHatch(true);
    }

    private Polyline CreateHatchPolyline(Point3d insertPt, LayerPosition layer)
    {
        var pline = new Polyline();
        pline.AddVertexAt(0, new Point2d(insertPt.X, insertPt.Y + layer.BottomStart), 0, 0, 0);
        pline.AddVertexAt(1, new Point2d(insertPt.X + 100, insertPt.Y + layer.BottomEnd), 0, 0, 0);
        pline.AddVertexAt(2, new Point2d(insertPt.X + 100, insertPt.Y + layer.TopEnd), 0, 0, 0);
        pline.AddVertexAt(3, new Point2d(insertPt.X, insertPt.Y + layer.TopStart), 0, 0, 0);
        pline.Closed = true;
        return pline;
    }

    private void DrawLine(BlockTableRecord btr, Point3d insertPt, Point2d start, Point2d end)
    {
        var line = new Line(
            new Point3d(insertPt.X + start.X, insertPt.Y + start.Y, insertPt.Z),
            new Point3d(insertPt.X + end.X, insertPt.Y + end.Y, insertPt.Z));
        btr.AppendEntity(line);
    }

    private void DrawAnnotations(BlockTableRecord btr, Point3d insertPt, double length, double maxY, List<double> intersections, Transaction tr)
    {
        var textY = maxY + 200;

        for (int i = 0; i < intersections.Count; i++)
        {
            DrawText(btr, insertPt, new Point2d(intersections[i], textY), $"交点{i + 1}", 150, tr);
        }

        DrawDimension(btr, insertPt, new Point2d(0, maxY + 500), new Point2d(length, maxY + 500), "总长", tr);
    }

    private void DrawText(BlockTableRecord btr, Point3d insertPt, Point2d pos, string text, double height, Transaction tr)
    {
        var dbText = new DBText
        {
            Position = new Point3d(insertPt.X + pos.X, insertPt.Y + pos.Y, insertPt.Z),
            TextString = text,
            Height = height
        };
        btr.AppendEntity(dbText);
        tr.AddNewlyCreatedDBObject(dbText, true);
    }

    private void DrawDimension(BlockTableRecord btr, Point3d insertPt, Point2d start, Point2d end, string text, Transaction tr)
    {
        var dim = new AlignedDimension(
            new Point3d(insertPt.X + start.X, insertPt.Y + start.Y, insertPt.Z),
            new Point3d(insertPt.X + end.X, insertPt.Y + end.Y, insertPt.Z),
            new Point3d(insertPt.X + start.X, insertPt.Y + start.Y + 200, insertPt.Z),
            text, ObjectId.Null);
        btr.AppendEntity(dim);
        tr.AddNewlyCreatedDBObject(dim, true);
    }

    private record LayerPosition(string Name, double BottomStart, double BottomEnd, double TopStart, double TopEnd, string HatchPattern);
}
