using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;

namespace MetroToolKits.Foundation.Cad.Services;

/// <summary>
/// 文档服务接口 - 封装 AutoCAD 文档操作
/// </summary>
public interface IDocumentService
{
    /// <summary>
    /// 获取当前数据库
    /// </summary>
    Database? GetCurrentDatabase();

    /// <summary>
    /// 启动事务
    /// </summary>
    Transaction StartTransaction();

    /// <summary>
    /// 锁定文档
    /// </summary>
    IDisposable? LockDocument();
}
