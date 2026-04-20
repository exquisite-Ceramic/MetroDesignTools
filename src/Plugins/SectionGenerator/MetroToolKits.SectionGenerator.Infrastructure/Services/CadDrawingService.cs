using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using MetroToolKits.Foundation.Core.Geometry;
using MetroToolKits.SectionGenerator.App.Abstractions;
using MetroToolKits.SectionGenerator.Core.Sections;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;

namespace MetroToolKits.SectionGenerator.Infrastructure.Services;

/// <summary>
/// AutoCAD 绘图服务 - 将剖面数据绘制为块并插入图纸
/// （原 CadAdapter 层，已合并至 Infrastructure）
/// </summary>
public sealed class CadDrawingService : IDrawingService
{
    private const string SectionSnapshotAppName = "MK_SectionSnapshot";
    private const string SectionSourceRefAppName = "MK_SectionSourceRef";

    // ── 单层 ──────────────────────────────────────────────────────────────────

    public DrawSectionBlockResult DrawSectionBlock(SectionGeometryData geometryData, Point3D insertionPoint, FloorConfig floorConfig)
    {
        var multiData = new MultiFloorSectionData
        {
            Floors      = new[] { geometryData },
            TotalHeight = floorConfig.Height + floorConfig.TopSlabThickness + floorConfig.BottomSlabThickness
        };
        return DrawMultiFloorSectionBlock(multiData, insertionPoint, new[] { floorConfig });
    }

    // ── 多楼层 ────────────────────────────────────────────────────────────────

    public DrawSectionBlockResult DrawMultiFloorSectionBlock(MultiFloorSectionData multiData, Point3D insertionPoint,
        IReadOnlyList<FloorConfig> floors)
    {
        var doc = Application.DocumentManager.MdiActiveDocument
            ?? throw new InvalidOperationException("无活动文档");

        var db        = doc.Database;
        var blockName = $"MK_剖面_{string.Join("_", floors.Select(f => f.Name))}_{DateTime.Now:yyyyMMddTHHmmss}";

        using var lockDoc = doc.LockDocument();
        using var tr      = db.TransactionManager.StartTransaction();

        EnsureLayers(tr, db);
        EnsureRegApp(tr, db, SectionSnapshotAppName);
        EnsureRegApp(tr, db, SectionSourceRefAppName);

        var bt       = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForWrite);
        var blockDef = new BlockTableRecord { Name = blockName, Origin = Point3d.Origin };
        bt.Add(blockDef);
        tr.AddNewlyCreatedDBObject(blockDef, true);

        foreach (var floorData in multiData.Floors)
        {
            foreach (var element in floorData.Elements)
            {
                foreach (var line in element.CutLines)
                    AddSourceAwareLine(blockDef, tr, line, "MK_剖切线", LineWeight.LineWeight050, element);

                foreach (var line in element.SightLines)
                    AddSourceAwareLine(blockDef, tr, line, "MK_看线", LineWeight.LineWeight025, element);
            }

            foreach (var line in floorData.SlabLines)
                AddLine(blockDef, tr, line, "MK_剖切线", LineWeight.LineWeight050);
        }

        DrawFloorAnnotations(blockDef, tr, multiData, floors);

        var ms       = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
        var blockRef = new BlockReference(
            new Point3d(insertionPoint.X, insertionPoint.Y, insertionPoint.Z),
            blockDef.ObjectId);
        ms.AppendEntity(blockRef);
        tr.AddNewlyCreatedDBObject(blockRef, true);

        AttachSnapshotXData(blockRef, tr, multiData, db);

        tr.Commit();
        return new DrawSectionBlockResult
        {
            BlockName = blockName,
            BlockHandle = blockRef.Handle.ToString()
        };
    }

    // ── 标注 ──────────────────────────────────────────────────────────────────

    private static void DrawFloorAnnotations(BlockTableRecord btr, Transaction tr,
        MultiFloorSectionData multiData, IReadOnlyList<FloorConfig> floors)
    {
        const double dimOffsetX = -800;

        for (int i = 0; i < multiData.Floors.Count; i++)
        {
            var floorData = multiData.Floors[i];
            var config    = floors[i];
            double baseZ  = floorData.BaseElevation;
            double topZ   = baseZ + config.Height;

            var dim = new AlignedDimension(
                new Point3d(dimOffsetX, baseZ, 0),
                new Point3d(dimOffsetX, topZ, 0),
                new Point3d(dimOffsetX - 300, (baseZ + topZ) / 2, 0),
                string.Empty, ObjectId.Null);
            btr.AppendEntity(dim);
            tr.AddNewlyCreatedDBObject(dim, true);

            AddText(btr, tr, new Point3d(dimOffsetX - 600, (baseZ + topZ) / 2, 0), config.Name, 150);

            double elevM = baseZ / 1000.0;
            AddText(btr, tr, new Point3d(200, baseZ, 0),
                elevM >= 0 ? $"+{elevM:F3}" : $"{elevM:F3}", 120);

            if (config.HasSlope && config.SlopeValue > 0)
                AddText(btr, tr,
                    new Point3d(0, topZ + config.TopSlabThickness + 200, 0),
                    $"i={config.SlopeValue * 100:F1}%", 100);
        }

        if (multiData.Floors.Count > 1)
        {
            double totalH = multiData.TotalHeight;
            var totalDim  = new AlignedDimension(
                new Point3d(dimOffsetX - 600, 0, 0),
                new Point3d(dimOffsetX - 600, totalH, 0),
                new Point3d(dimOffsetX - 900, totalH / 2, 0),
                string.Empty, ObjectId.Null);
            btr.AppendEntity(totalDim);
            tr.AddNewlyCreatedDBObject(totalDim, true);
        }
    }

    // ── 辅助 ──────────────────────────────────────────────────────────────────

    private static void EnsureLayers(Transaction tr, Database db)
    {
        var lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
        foreach (var name in new[] { "MK_剖切线", "MK_看线", "MK_标注" })
        {
            if (lt.Has(name)) continue;
            lt.UpgradeOpen();
            var layer = new LayerTableRecord { Name = name };
            lt.Add(layer);
            tr.AddNewlyCreatedDBObject(layer, true);
        }
    }

    private static void AddLine(BlockTableRecord btr, Transaction tr,
        Line3D line, string layer, LineWeight lineWeight)
    {
        var cadLine = new Line(
            new Point3d(line.Start.X, line.Start.Y, line.Start.Z),
            new Point3d(line.End.X,   line.End.Y,   line.End.Z))
        { Layer = layer, LineWeight = lineWeight };
        btr.AppendEntity(cadLine);
        tr.AddNewlyCreatedDBObject(cadLine, true);
    }

    private static void AddSourceAwareLine(BlockTableRecord btr, Transaction tr,
        Line3D line, string layer, LineWeight lineWeight, ElementSectionData element)
    {
        var cadLine = new Line(
            new Point3d(line.Start.X, line.Start.Y, line.Start.Z),
            new Point3d(line.End.X, line.End.Y, line.End.Z))
        {
            Layer = layer,
            LineWeight = lineWeight
        };

        btr.AppendEntity(cadLine);
        tr.AddNewlyCreatedDBObject(cadLine, true);

        if (!string.IsNullOrWhiteSpace(element.SourceHandle))
        {
            cadLine.XData = new ResultBuffer(
                new TypedValue((int)DxfCode.ExtendedDataRegAppName, SectionSourceRefAppName),
                new TypedValue((int)DxfCode.ExtendedDataAsciiString, element.SourceHandle),
                new TypedValue((int)DxfCode.ExtendedDataAsciiString, element.ElementType));
        }
    }

    private static void AddText(BlockTableRecord btr, Transaction tr,
        Point3d position, string content, double height)
    {
        var text = new DBText
        { Position = position, TextString = content, Height = height, Layer = "MK_标注" };
        btr.AppendEntity(text);
        tr.AddNewlyCreatedDBObject(text, true);
    }

    private static void AttachSnapshotXData(BlockReference blockRef, Transaction tr,
        MultiFloorSectionData data, Database db)
    {
        var floorNames = string.Join(",", data.Floors.Select(f => f.FloorName));
        blockRef.XData = new ResultBuffer(
            new TypedValue((int)DxfCode.ExtendedDataRegAppName, SectionSnapshotAppName),
            new TypedValue((int)DxfCode.ExtendedDataAsciiString, floorNames),
            new TypedValue((int)DxfCode.ExtendedDataAsciiString, DateTime.Now.ToString("O")),
            new TypedValue((int)DxfCode.ExtendedDataReal, data.TotalHeight));
    }

    private static void EnsureRegApp(Transaction tr, Database db, string appName)
    {
        var rat = (RegAppTable)tr.GetObject(db.RegAppTableId, OpenMode.ForRead);
        if (rat.Has(appName)) return;

        rat.UpgradeOpen();
        var rec = new RegAppTableRecord { Name = appName };
        rat.Add(rec);
        tr.AddNewlyCreatedDBObject(rec, true);
    }
}
