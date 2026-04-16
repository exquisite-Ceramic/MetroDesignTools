using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;

namespace MetroToolKits.Foundation.Cad.Services;

/// <summary>
/// 事务服务实现
/// </summary>
public class TransactionService : ITransactionService
{
    private readonly IDocumentService _documentService;

    public TransactionService(IDocumentService documentService)
    {
        _documentService = documentService;
    }

    public void Execute(Action<Transaction> action)
    {
        var db = _documentService.GetCurrentDatabase();
        if (db == null) return;

        using var tr = db.TransactionManager.StartTransaction();
        try
        {
            action(tr);
            tr.Commit();
        }
        catch
        {
            tr.Abort();
            throw;
        }
    }

    public T? Execute<T>(Func<Transaction, T> func)
    {
        var db = _documentService.GetCurrentDatabase();
        if (db == null) return default;

        using var tr = db.TransactionManager.StartTransaction();
        try
        {
            var result = func(tr);
            tr.Commit();
            return result;
        }
        catch
        {
            tr.Abort();
            throw;
        }
    }

    public void ExecuteWithLock(Action<Transaction> action)
    {
        var doc = Application.DocumentManager.MdiActiveDocument;
        if (doc == null) return;

        using var lockDoc = doc.LockDocument();
        Execute(action);
    }

    public T? ExecuteWithLock<T>(Func<Transaction, T> func)
    {
        var doc = Application.DocumentManager.MdiActiveDocument;
        if (doc == null) return default;

        using var lockDoc = doc.LockDocument();
        return Execute(func);
    }
}
