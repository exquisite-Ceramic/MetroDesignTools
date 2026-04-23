using Autodesk.AutoCAD.DatabaseServices;

namespace MetroToolKits.Foundation.Cad.Layering.Services;

/// <summary>
/// 提供当前活动图纸数据库访问能力
/// </summary>
public interface ICadDatabaseAccessor
{
    /// <summary>
    /// 获取当前数据库
    /// </summary>
    Database? GetCurrentDatabase();
}
