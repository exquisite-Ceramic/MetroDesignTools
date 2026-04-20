using Autodesk.AutoCAD.DatabaseServices;

namespace MetroToolKits.Foundation.Cad.Services;

/// <summary>
/// 图层服务接口 - 封装 AutoCAD 图层操作
/// </summary>
public interface ILayerService
{
    /// <summary>
    /// 获取或创建图层
    /// </summary>
    /// <param name="layerName">图层名称</param>
    /// <returns>图层 ObjectId</returns>
    ObjectId GetOrCreateLayer(string layerName);

    /// <summary>
    /// 设置实体图层
    /// </summary>
    void SetEntityLayer(Entity entity, string layerName);

    /// <summary>
    /// 设置图层颜色
    /// </summary>
    void SetLayerColor(string layerName, short colorIndex);

    /// <summary>
    /// 设置图层线型
    /// </summary>
    void SetLayerLinetype(string layerName, string linetypeName);

    /// <summary>
    /// 获取所有图层名称
    /// </summary>
    IEnumerable<string> GetAllLayerNames();

    /// <summary>
    /// 图层是否存在
    /// </summary>
    bool LayerExists(string layerName);
}
