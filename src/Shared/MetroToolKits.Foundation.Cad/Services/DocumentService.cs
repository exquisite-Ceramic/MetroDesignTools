using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using MetroToolKits.Foundation.Cad.Layering.Services;

namespace MetroToolKits.Foundation.Cad.Services;

/// <summary>
/// 文档服务实现
/// </summary>
public class DocumentService : IDocumentService, ICadDatabaseAccessor
{
    public Database? GetCurrentDatabase()
    {
        var doc = Application.DocumentManager.MdiActiveDocument;
        return doc?.Database;
    }

    public Transaction StartTransaction()
    {
        var db = GetCurrentDatabase();
        if (db == null)
            throw new InvalidOperationException("无法获取当前数据库");
        
        return db.TransactionManager.StartTransaction();
    }

    public IDisposable? LockDocument()
    {
        var doc = Application.DocumentManager.MdiActiveDocument;
        return doc?.LockDocument();
    }
}
