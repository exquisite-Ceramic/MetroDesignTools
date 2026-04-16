using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using MetroToolKits.Foundation.Core.Geometry;
using MetroToolKits.SectionGenerator.App.Abstractions;
using MetroToolKits.SectionGenerator.Core.Sections;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;

namespace MetroToolKits.SectionGenerator.CadAdapter;

/// <summary>
/// AutoCAD 绘图服务 - 将 SectionGeometryData 绘制为块并插入图纸
/// </summary>
public sealed class CadDrawingService : IDrawingService
{
    public string DrawSectionBlock(SectionGeometryData geometryData, Point3D insertionPoint, FloorConfig floorConfig)
    {
        var doc = Application.DocumentManager.MdiActiveDocument
            ?? throw new InvalidOperationException("无活动文档");

        var db = doc.Database;
        var blockName = $"MK_剖面_{geometryData.FloorName}_{DateTime.Now:yyyyMMddTHHmmss}";

        using var lockDoc = doc.LockDocument();
        using var tr = db.TransactionManager.StartTransaction();

        // 1. 确保图层存在
        EnsureLayer(tr, db, "MK_剖切线");
        EnsureLayer(tr, db, "MK_看线");
        EnsureLayer(tr, db, "MK_标注");

        // 2. 创建块定义
        var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForWrite);
        var blockDef = new BlockTableRecord { Name = blockName, Origin = Point3d.Origin };
        bt.Add(blockDef);
        tr.AddNewlyCreatedDBObject(blockDef, true);

        // 3. 绘制剖切线（粗实线）
        foreach (var line in geometryData.AllCutLines)
            AddLine(blockDef, tr, line, "MK_剖切线", LineWeight.LineWeight050);

        // 4. 绘制看线（细实线）
        foreach (var line in geometryData.AllSightLines)
            AddLine(blockDef, tr, line, "MK_看线", LineWeight.LineWeight025);

        // 5. 绘制标注
        DrawAnnotations(blockDef, tr, geometryData, floorConfig);

        // 6. 将块插入模型空间
        var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
        var blockRef = new BlockReference(
            new Point3d(insertionPoint.X, insertionPoint.Y, insertionPoint.Z),
            blockDef.ObjectId);
        ms.AppendEntity(blockRef);
        tr.AddNewlyCreatedDBObject(blockRef, true);

        // 7. 附加快照 XData（供变更检测使用）
        AttachSnapshotXData(blockRef, tr, geometryData, db);

        tr.Commit();
        return blockName;
    }

    private static void EnsureLayer(Transaction tr, Database db, string layerName)
    {
        var lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
        if (lt.Has(layerName)) return;
        lt.UpgradeOpen();
        var layer = new LayerTableRecord { Name = layerName };
        lt.Add(layer);
        tr.AddNewlyCreatedDBObject(layer, true);
    }

    private static void AddLine(BlockTableRecord btr, Transaction tr,
        Line3D line, string layer, LineWeight lineWeight)
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
    }

    private static void DrawAnnotations(BlockTableRecord btr, Transaction tr,
        SectionGeometryData data, FloorConfig config)
    {
        double baseZ = data.BaseElevation;
        double topZ = baseZ + config.Height;
        const double dimOffsetX = -800;

        // 层高标注（对齐标注）
        var dim = new AlignedDimension(
            new Point3d(dimOffsetX, baseZ, 0),
            new Point3d(dimOffsetX, topZ, 0),
            new Point3d(dimOffsetX - 300, (baseZ + topZ) / 2, 0),
            string.Empty, ObjectId.Null);
        btr.AppendEntity(dim);
        tr.AddNewlyCreatedDBObject(dim, true);

        // 底板标高文字
        AddText(btr, tr, new Point3d(200, baseZ, 0), "±0.000", 150);

        // 顶板标高文字
        double topElevM = (config.Height + config.TopSlabThickness) / 1000.0;
        AddText(btr, tr, new Point3d(200, topZ, 0), $"+{topElevM:F3}", 150);

        // 坡度标注
        if (config.HasSlope && config.SlopeValue > 0)
            AddText(btr, tr,
                new Point3d(0, topZ + config.TopSlabThickness + 200, 0),
                $"i={config.SlopeValue * 100:F1}%", 120);
    }

    private static void AddText(BlockTableRecord btr, Transaction tr,
        Point3d position, string content, double height)
    {
        var text = new DBText
        {
            Position = position,
            TextString = content,
            Height = height,
            Layer = "MK_标注"
        };
        btr.AppendEntity(text);
        tr.AddNewlyCreatedDBObject(text, true);
    }

    private static void AttachSnapshotXData(BlockReference blockRef, Transaction tr,
        SectionGeometryData data, Database db)
    {
        const string appName = "MK_SectionSnapshot";
        var rat = (RegAppTable)tr.GetObject(db.RegAppTableId, OpenMode.ForRead);
        if (!rat.Has(appName))
        {
            rat.UpgradeOpen();
            var rec = new RegAppTableRecord { Name = appName };
            rat.Add(rec);
            tr.AddNewlyCreatedDBObject(rec, true);
        }

        blockRef.XData = new ResultBuffer(
            new TypedValue((int)DxfCode.ExtendedDataRegAppName, appName),
            new TypedValue((int)DxfCode.ExtendedDataAsciiString, data.FloorName),
            new TypedValue((int)DxfCode.ExtendedDataAsciiString, DateTime.Now.ToString("O")));
    }
}
