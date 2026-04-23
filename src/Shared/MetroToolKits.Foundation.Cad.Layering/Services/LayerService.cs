using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;

namespace MetroToolKits.Foundation.Cad.Layering.Services;

/// <summary>
/// 图层服务实现
/// </summary>
public class LayerService : ILayerService
{
    private readonly ICadDatabaseAccessor _databaseAccessor;

    public LayerService(ICadDatabaseAccessor databaseAccessor)
    {
        _databaseAccessor = databaseAccessor;
    }

    public ObjectId GetOrCreateLayer(string layerName)
    {
        var db = _databaseAccessor.GetCurrentDatabase();
        if (db == null) return ObjectId.Null;

        using var tr = db.TransactionManager.StartTransaction();
        var layerTable = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);

        if (layerTable.Has(layerName))
        {
            return layerTable[layerName];
        }

        layerTable.UpgradeOpen();
        var layer = new LayerTableRecord { Name = layerName };
        layerTable.Add(layer);
        tr.AddNewlyCreatedDBObject(layer, true);
        tr.Commit();

        return layer.ObjectId;
    }

    public void SetEntityLayer(Entity entity, string layerName)
    {
        if (entity == null) return;

        var layerId = GetOrCreateLayer(layerName);
        if (layerId != ObjectId.Null)
        {
            entity.LayerId = layerId;
        }
    }

    public void SetLayerColor(string layerName, short colorIndex)
    {
        var db = _databaseAccessor.GetCurrentDatabase();
        if (db == null) return;

        using var tr = db.TransactionManager.StartTransaction();
        var layerTable = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);

        if (layerTable.Has(layerName))
        {
            var layer = (LayerTableRecord)tr.GetObject(layerTable[layerName], OpenMode.ForWrite);
            layer.Color = Color.FromColorIndex(ColorMethod.ByAci, colorIndex);
            tr.Commit();
        }
    }

    public void SetLayerLinetype(string layerName, string linetypeName)
    {
        var db = _databaseAccessor.GetCurrentDatabase();
        if (db == null) return;

        using var tr = db.TransactionManager.StartTransaction();
        var layerTable = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);

        if (layerTable.Has(layerName))
        {
            var layer = (LayerTableRecord)tr.GetObject(layerTable[layerName], OpenMode.ForWrite);
            layer.LinetypeObjectId = GetLinetypeId(tr, db, linetypeName);
            tr.Commit();
        }
    }

    public IEnumerable<string> GetAllLayerNames()
    {
        var db = _databaseAccessor.GetCurrentDatabase();
        if (db == null) return Enumerable.Empty<string>();

        using var tr = db.TransactionManager.StartTransaction();
        var layerTable = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);

        return layerTable.Cast<ObjectId>()
            .Select(id => ((LayerTableRecord)tr.GetObject(id, OpenMode.ForRead)).Name)
            .ToList();
    }

    public bool LayerExists(string layerName)
    {
        var db = _databaseAccessor.GetCurrentDatabase();
        if (db == null) return false;

        using var tr = db.TransactionManager.StartTransaction();
        var layerTable = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
        return layerTable.Has(layerName);
    }

    private static ObjectId GetLinetypeId(Transaction tr, Database db, string linetypeName)
    {
        var ltTable = (LinetypeTable)tr.GetObject(db.LinetypeTableId, OpenMode.ForRead);
        return ltTable.Has(linetypeName) ? ltTable[linetypeName] : ObjectId.Null;
    }
}
