using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Runtime;
using Microsoft.Extensions.Logging;
using MetroToolKits.Foundation.Core.Geometry;
using MetroToolKits.SectionGenerator.App.Abstractions;
using MetroToolKits.SectionGenerator.Core.Sections;
using Newtonsoft.Json;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;
using SystemException = System.Exception;

namespace MetroToolKits.SectionGenerator.Infrastructure.Repositories;

/// <summary>
/// 基于 XData 的剖面快照仓储
/// 快照序列化为 JSON 字符串存入块参照的扩展数据
/// </summary>
public sealed class XDataSnapshotRepository : ISectionSnapshotRepository
{
    private const string AppName = "MK_SectionSnapshot_V2";
    private readonly ILogger<XDataSnapshotRepository> _logger;

    public XDataSnapshotRepository(ILogger<XDataSnapshotRepository> logger)
    {
        _logger = logger;
    }

    public void Save(string blockHandle, SectionSnapshot snapshot)
    {
        var doc = Application.DocumentManager.MdiActiveDocument;
        if (doc == null) return;

        var db = doc.Database;
        using var lockDoc = doc.LockDocument();
        using var tr = db.TransactionManager.StartTransaction();

        try
        {
            EnsureAppRegistered(tr, db);

            var objId = db.GetObjectId(false, new Handle(Convert.ToInt64(blockHandle, 16)), 0);
            if (objId == ObjectId.Null)
            {
                _logger.LogWarning("找不到块句柄 {Handle}", blockHandle);
                tr.Abort();
                return;
            }

            var blockRef = (BlockReference)tr.GetObject(objId, OpenMode.ForWrite);
            var json = JsonConvert.SerializeObject(snapshot);

            blockRef.XData = new ResultBuffer(
                new TypedValue((int)DxfCode.ExtendedDataRegAppName, AppName),
                new TypedValue((int)DxfCode.ExtendedDataAsciiString, json));

            tr.Commit();
            _logger.LogDebug("写入剖面快照，块: {BlockName}，楼层数: {FloorCount}",
                snapshot.BlockName, snapshot.FloorSnapshots.Count);
        }
        catch (SystemException ex)
        {
            tr.Abort();
            _logger.LogError(ex, "写入快照失败，块句柄: {Handle}", blockHandle);
        }
    }

    public SectionSnapshot? Load(string blockHandle)
    {
        var doc = Application.DocumentManager.MdiActiveDocument;
        if (doc == null) return null;

        var db = doc.Database;
        using var tr = db.TransactionManager.StartTransaction();

        try
        {
            var objId = db.GetObjectId(false, new Handle(Convert.ToInt64(blockHandle, 16)), 0);
            if (objId == ObjectId.Null)
            {
                tr.Abort();
                return null;
            }

            var blockRef = (BlockReference)tr.GetObject(objId, OpenMode.ForRead);
            var xdata = blockRef.GetXDataForApplication(AppName);

            if (xdata == null)
            {
                tr.Abort();
                return null;
            }

            var values = xdata.AsArray();
            // values[0] = AppName, values[1] = JSON
            if (values.Length < 2)
            {
                tr.Abort();
                return null;
            }

            var json = values[1].Value?.ToString();
            tr.Commit();

            if (string.IsNullOrEmpty(json)) return null;
            return JsonConvert.DeserializeObject<SectionSnapshot>(json);
        }
        catch (SystemException ex)
        {
            tr.Abort();
            _logger.LogError(ex, "读取快照失败，块句柄: {Handle}", blockHandle);
            return null;
        }
    }

    public IReadOnlyList<string> FindAllSectionBlockHandles()
    {
        var doc = Application.DocumentManager.MdiActiveDocument;
        if (doc == null) return Array.Empty<string>();

        var db = doc.Database;
        var handles = new List<string>();

        using var tr = db.TransactionManager.StartTransaction();
        var bt  = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
        var ms  = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);

        foreach (var objId in ms)
        {
            if (tr.GetObject(objId, OpenMode.ForRead) is not BlockReference blockRef) continue;

            // 检查是否有我们的 XData
            var xdata = blockRef.GetXDataForApplication(AppName);
            if (xdata != null)
                handles.Add(blockRef.Handle.ToString());
        }

        tr.Commit();
        _logger.LogDebug("扫描到 {Count} 个剖面块", handles.Count);
        return handles;
    }

    private static void EnsureAppRegistered(Transaction tr, Database db)
    {
        var rat = (RegAppTable)tr.GetObject(db.RegAppTableId, OpenMode.ForRead);
        if (rat.Has(AppName)) return;
        rat.UpgradeOpen();
        var rec = new RegAppTableRecord { Name = AppName };
        rat.Add(rec);
        tr.AddNewlyCreatedDBObject(rec, true);
    }
}
