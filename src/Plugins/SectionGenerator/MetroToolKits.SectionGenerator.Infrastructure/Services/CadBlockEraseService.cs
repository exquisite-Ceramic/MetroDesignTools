using Autodesk.AutoCAD.DatabaseServices;
using Microsoft.Extensions.Logging;
using MetroToolKits.SectionGenerator.App.Abstractions;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;

namespace MetroToolKits.SectionGenerator.Infrastructure.Services;

/// <summary>
/// CAD 块删除服务
/// </summary>
public sealed class CadBlockEraseService : IBlockEraseService
{
    private readonly ILogger<CadBlockEraseService> _logger;

    public CadBlockEraseService(ILogger<CadBlockEraseService> logger)
    {
        _logger = logger;
    }

    public void EraseBlock(string blockHandle)
    {
        var doc = Application.DocumentManager.MdiActiveDocument;
        if (doc == null) return;

        var db = doc.Database;
        using var lockDoc = doc.LockDocument();
        using var tr = db.TransactionManager.StartTransaction();

        try
        {
            var objId = db.GetObjectId(false,
                new Handle(Convert.ToInt64(blockHandle, 16)), 0);

            if (objId == ObjectId.Null)
            {
                _logger.LogWarning("找不到要删除的块，句柄: {Handle}", blockHandle);
                tr.Abort();
                return;
            }

            var blockRef = (BlockReference)tr.GetObject(objId, OpenMode.ForWrite);
            blockRef.Erase();
            tr.Commit();
            _logger.LogDebug("已删除块，句柄: {Handle}", blockHandle);
        }
        catch (Exception ex)
        {
            tr.Abort();
            _logger.LogError(ex, "删除块失败，句柄: {Handle}", blockHandle);
        }
    }
}
