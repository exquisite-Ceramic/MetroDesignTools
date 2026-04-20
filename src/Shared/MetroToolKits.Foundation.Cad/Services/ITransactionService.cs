using Autodesk.AutoCAD.DatabaseServices;

namespace MetroToolKits.Foundation.Cad.Services;

/// <summary>
/// 事务服务接口 - 封装 AutoCAD 事务操作
/// </summary>
public interface ITransactionService
{
    /// <summary>
    /// 在事务中执行操作
    /// </summary>
    /// <param name="action">要执行的操作</param>
    void Execute(Action<Transaction> action);

    /// <summary>
    /// 在事务中执行操作并返回结果
    /// </summary>
    T? Execute<T>(Func<Transaction, T> func);

    /// <summary>
    /// 在事务中执行操作（带文档锁定）
    /// </summary>
    void ExecuteWithLock(Action<Transaction> action);

    /// <summary>
    /// 在事务中执行操作并返回结果（带文档锁定）
    /// </summary>
    T? ExecuteWithLock<T>(Func<Transaction, T> func);
}
