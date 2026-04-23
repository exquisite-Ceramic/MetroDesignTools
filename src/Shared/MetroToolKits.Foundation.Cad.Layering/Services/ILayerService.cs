using Autodesk.AutoCAD.DatabaseServices;

namespace MetroToolKits.Foundation.Cad.Layering.Services;

/// <summary>
/// 图层服务接口 - 封装 AutoCAD 图层操作
/// </summary>
public interface ILayerService
{
    ObjectId GetOrCreateLayer(string layerName);

    void SetEntityLayer(Entity entity, string layerName);

    void SetLayerColor(string layerName, short colorIndex);

    void SetLayerLinetype(string layerName, string linetypeName);

    IEnumerable<string> GetAllLayerNames();

    bool LayerExists(string layerName);
}
