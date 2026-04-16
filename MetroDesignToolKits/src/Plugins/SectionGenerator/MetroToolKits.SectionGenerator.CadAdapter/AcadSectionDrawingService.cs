using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using SectionGenerator.App.Abstractions;
using SectionGenerator.Core.Sections;

namespace SectionGenerator.CadAdapter;

/// <summary>
/// AutoCAD 剖面绘制服务实现 - 基础设施层
/// </summary>
public sealed class AcadSectionDrawingService : ISectionDrawingService
{
    private readonly Database _database;

    public AcadSectionDrawingService(Database database)
    {
        _database = database;
    }

    public void DrawSection(SectionResult result, IReadOnlyList<double> intersectionParams)
    {
        using Transaction tr = _database.TransactionManager.StartTransaction();
        BlockTable bt = (BlockTable)tr.GetObject(_database.BlockTableId, OpenMode.ForRead);
        BlockTableRecord btr = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);

        // 绘制各层
        foreach (var layer in result.Layers)
        {
            DrawLayer(btr, layer, tr);
        }

        // 绘制交点线
        double maxY = result.Layers.Max(l => System.Math.Max(l.TopStart, l.TopEnd));
        double minY = result.Layers.Min(l => System.Math.Min(l.BottomStart, l.BottomEnd));

        foreach (double param in intersectionParams)
        {
            DrawLine(btr, new Point2d(param, minY), new Point2d(param, maxY));
        }

        // 绘制标注
        DrawAnnotations(btr, result, intersectionParams, maxY, tr);

        tr.Commit();
    }

    private void DrawLayer(BlockTableRecord btr, LayerPosition layer, Transaction tr)
    {
        // 绘制层的填充
        DrawHatchBetweenLines(btr,
            new Point2d(0, layer.BottomStart),
            new Point2d(0, layer.BottomEnd),
            new Point2d(0, layer.TopStart),
            new Point2d(0, layer.TopEnd),
            layer.HatchPattern, tr);

        // 绘制层的边界线
        DrawLine(btr, new Point2d(0, layer.BottomStart), new Point2d(0, layer.BottomEnd));
        DrawLine(btr, new Point2d(0, layer.TopStart), new Point2d(0, layer.TopEnd));
    }

    private void DrawHatchBetweenLines(BlockTableRecord btr, Point2d bl, Point2d br, Point2d tl, Point2d trPt, string patternName, Transaction tr)
    {
        Polyline pline = new Polyline();
        pline.AddVertexAt(0, bl, 0, 0, 0);
        pline.AddVertexAt(1, br, 0, 0, 0);
        pline.AddVertexAt(2, trPt, 0, 0, 0);
        pline.AddVertexAt(3, tl, 0, 0, 0);
        pline.Closed = true;
        btr.AppendEntity(pline);
        tr.AddNewlyCreatedDBObject(pline, true);

        Hatch hatch = new Hatch();
        btr.AppendEntity(hatch);
        tr.AddNewlyCreatedDBObject(hatch, true);
        hatch.SetHatchPattern(HatchPatternType.PreDefined, patternName);
        hatch.PatternScale = 50;
        hatch.Associative = true;
        hatch.AppendLoop(HatchLoopTypes.External, new ObjectIdCollection { pline.ObjectId });
        hatch.EvaluateHatch(true);
    }

    private void DrawLine(BlockTableRecord btr, Point2d start, Point2d end)
    {
        Line line = new Line(
            new Point3d(start.X, start.Y, 0),
            new Point3d(end.X, end.Y, 0));
        btr.AppendEntity(line);
    }

    private void DrawAnnotations(BlockTableRecord btr, SectionResult result, IReadOnlyList<double> intersectionParams, double maxY, Transaction tr)
    {
        double textY = maxY + 200;

        // 绘制交点标注
        for (int i = 0; i < intersectionParams.Count; i++)
        {
            double x = intersectionParams[i];
            string text = $"交点{i + 1}";
            DrawText(btr, new Point2d(x, textY), text, 150, tr);
        }

        // 绘制总长标注
        double dimY = maxY + 500;
        double length = result.SectionPolyline.Vertices.Count > 1 
            ? result.SectionPolyline.Vertices[1].X - result.SectionPolyline.Vertices[0].X 
            : 0;
        DrawDimension(btr, new Point2d(0, dimY), new Point2d(length, dimY), "总长", tr);
    }

    private void DrawText(BlockTableRecord btr, Point2d position, string text, double height, Transaction tr)
    {
        DBText dbText = new DBText();
        dbText.Position = new Point3d(position.X, position.Y, 0);
        dbText.TextString = text;
        dbText.Height = height;
        btr.AppendEntity(dbText);
        tr.AddNewlyCreatedDBObject(dbText, true);
    }

    private void DrawDimension(BlockTableRecord btr, Point2d start, Point2d end, string text, Transaction tr)
    {
        AlignedDimension dim = new AlignedDimension(
            new Point3d(start.X, start.Y, 0),
            new Point3d(end.X, end.Y, 0),
            new Point3d(start.X, start.Y + 200, 0),
            text, ObjectId.Null);
        btr.AppendEntity(dim);
        tr.AddNewlyCreatedDBObject(dim, true);
    }
}
