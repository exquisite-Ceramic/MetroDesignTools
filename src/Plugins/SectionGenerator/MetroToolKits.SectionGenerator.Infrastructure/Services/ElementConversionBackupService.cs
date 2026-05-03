using Autodesk.AutoCAD.DatabaseServices;

namespace MetroToolKits.SectionGenerator.Infrastructure.Services;

/// <summary>
/// 构件转换备份服务 - 将实体原始属性序列化存入图纸扩展字典
/// </summary>
public sealed class ElementConversionBackupService
{
    private const string BackupDictKey = "MK_ElementBackup";
    private const string OriginalLayerKey = "OriginalLayer";
    private const string OriginalColorKey = "OriginalColor";
    private const string OriginalLinetypeKey = "OriginalLinetype";
    private const string OriginalLineweightKey = "OriginalLineweight";
    private const string ConvertedTypeKey = "ConvertedType";
    private const string TemplateIdKey = "TemplateId";
    private const string ConvertedTimeKey = "ConvertedTime";

    /// <summary>
    /// 备份实体原始属性
    /// </summary>
    public void BackupEntity(Transaction tr, Entity entity, string convertedType, string? templateId = null)
    {
        var db = entity.Database;
        if (db == null)
        {
            return;
        }

        // 获取或创建扩展字典
        var dictId = GetOrCreateBackupDictionary(tr, db);
        if (dictId == ObjectId.Null)
        {
            return;
        }

        var dict = (DBDictionary)tr.GetObject(dictId, OpenMode.ForWrite);

        // 创建实体专属的扩展字典
        var entityDictId = GetOrCreateEntityDictionary(tr, dict, entity.Handle.ToString());
        if (entityDictId == ObjectId.Null)
        {
            return;
        }

        var entityDict = (DBDictionary)tr.GetObject(entityDictId, OpenMode.ForWrite);

        // 存储原始属性
        SetXrecordValue(tr, entityDict, OriginalLayerKey, entity.Layer);
        SetXrecordValue(tr, entityDict, OriginalColorKey, entity.ColorIndex.ToString());
        SetXrecordValue(tr, entityDict, OriginalLinetypeKey, entity.Linetype);
        SetXrecordValue(tr, entityDict, OriginalLineweightKey, ((int)entity.LineWeight).ToString());
        SetXrecordValue(tr, entityDict, ConvertedTypeKey, convertedType);
        SetXrecordValue(tr, entityDict, TemplateIdKey, templateId ?? string.Empty);
        SetXrecordValue(tr, entityDict, ConvertedTimeKey, DateTime.Now.ToString("O"));
    }

    public bool BackfillConversionMetadata(
        Transaction tr,
        Entity entity,
        string convertedType,
        string templateId,
        string? inferredOriginalLayer,
        bool overwriteTemplateId,
        out string? failureMessage)
    {
        failureMessage = null;
        if (string.IsNullOrWhiteSpace(convertedType))
        {
            failureMessage = "ConvertedType 不能为空。";
            return false;
        }

        if (string.IsNullOrWhiteSpace(templateId))
        {
            failureMessage = "TemplateId 不能为空。";
            return false;
        }

        var db = entity.Database;
        if (db == null)
        {
            failureMessage = "实体未关联到数据库。";
            return false;
        }

        var dictId = GetOrCreateBackupDictionary(tr, db);
        if (dictId == ObjectId.Null)
        {
            failureMessage = "无法创建转换备份字典。";
            return false;
        }

        var dict = (DBDictionary)tr.GetObject(dictId, OpenMode.ForWrite);
        var entityDictId = GetOrCreateEntityDictionary(tr, dict, entity.Handle.ToString());
        if (entityDictId == ObjectId.Null)
        {
            failureMessage = "无法创建实体转换元数据记录。";
            return false;
        }

        var entityDict = (DBDictionary)tr.GetObject(entityDictId, OpenMode.ForWrite);
        var existingConvertedType = NormalizeValue(GetXrecordValue(tr, entityDict, ConvertedTypeKey));
        if (!string.IsNullOrWhiteSpace(existingConvertedType) &&
            !string.Equals(existingConvertedType, convertedType, StringComparison.OrdinalIgnoreCase))
        {
            failureMessage = $"实体已有 ConvertedType={existingConvertedType}，不会覆盖为 {convertedType}。";
            return false;
        }

        var existingOriginalLayer = NormalizeValue(GetXrecordValue(tr, entityDict, OriginalLayerKey));
        if (string.IsNullOrWhiteSpace(existingOriginalLayer) &&
            !string.IsNullOrWhiteSpace(inferredOriginalLayer))
        {
            SetXrecordValue(tr, entityDict, OriginalLayerKey, inferredOriginalLayer);
        }

        SetXrecordValue(tr, entityDict, ConvertedTypeKey, convertedType);

        var existingTemplateId = NormalizeValue(GetXrecordValue(tr, entityDict, TemplateIdKey));
        if (string.IsNullOrWhiteSpace(existingTemplateId) || overwriteTemplateId)
        {
            SetXrecordValue(tr, entityDict, TemplateIdKey, templateId);
        }

        SetXrecordValue(tr, entityDict, ConvertedTimeKey, DateTime.Now.ToString("O"));
        return true;
    }

    /// <summary>
    /// 恢复实体原始属性
    /// </summary>
    public bool RestoreEntity(Transaction tr, Entity entity)
    {
        var db = entity.Database;
        if (db == null)
        {
            return false;
        }

        var dictId = GetBackupDictionary(tr, db);
        if (dictId == ObjectId.Null) return false;

        var dict = (DBDictionary)tr.GetObject(dictId, OpenMode.ForRead);
        var handleStr = entity.Handle.ToString();

        if (!dict.Contains(handleStr))
        {
            return false;
        }

        var entityDictId = dict.GetAt(handleStr);
        var entityDict = (DBDictionary)tr.GetObject(entityDictId, OpenMode.ForRead);

        // 读取并恢复原始属性
        var layer = GetXrecordValue(tr, entityDict, OriginalLayerKey);
        var colorStr = GetXrecordValue(tr, entityDict, OriginalColorKey);
        var linetype = GetXrecordValue(tr, entityDict, OriginalLinetypeKey);
        var lineweightStr = GetXrecordValue(tr, entityDict, OriginalLineweightKey);

        entity.UpgradeOpen();
        if (!string.IsNullOrEmpty(layer)) entity.Layer = layer;
        if (int.TryParse(colorStr, out var colorIndex)) entity.ColorIndex = colorIndex;
        if (!string.IsNullOrEmpty(linetype)) entity.Linetype = linetype;
        if (int.TryParse(lineweightStr, out var lw)) entity.LineWeight = (LineWeight)lw;

        // 删除备份记录
        dict.UpgradeOpen();
        dict.Remove(handleStr);
        return true;
    }

    /// <summary>
    /// 检查实体是否有备份
    /// </summary>
    public bool HasBackup(Transaction tr, Entity entity)
    {
        var db = entity.Database;
        if (db == null)
        {
            return false;
        }

        var dictId = GetBackupDictionary(tr, db);
        if (dictId == ObjectId.Null)
        {
            return false;
        }

        var dict = (DBDictionary)tr.GetObject(dictId, OpenMode.ForRead);
        return dict.Contains(entity.Handle.ToString());
    }

    /// <summary>
    /// 获取实体的转换类型
    /// </summary>
    public string? GetConvertedType(Transaction tr, Entity entity)
    {
        var db = entity.Database;
        if (db == null)
        {
            return null;
        }

        var dictId = GetBackupDictionary(tr, db);
        if (dictId == ObjectId.Null)
        {
            return null;
        }

        var dict = (DBDictionary)tr.GetObject(dictId, OpenMode.ForRead);
        var handleStr = entity.Handle.ToString();

        if (!dict.Contains(handleStr))
        {
            return null;
        }

        var entityDictId = dict.GetAt(handleStr);
        var entityDict = (DBDictionary)tr.GetObject(entityDictId, OpenMode.ForRead);
        return GetXrecordValue(tr, entityDict, ConvertedTypeKey);
    }

    /// <summary>
    /// 获取实体的模板标识。
    /// </summary>
    public string? GetTemplateId(Transaction tr, Entity entity)
    {
        var db = entity.Database;
        if (db == null)
        {
            return null;
        }

        var dictId = GetBackupDictionary(tr, db);
        if (dictId == ObjectId.Null)
        {
            return null;
        }

        var dict = (DBDictionary)tr.GetObject(dictId, OpenMode.ForRead);
        var handleStr = entity.Handle.ToString();

        if (!dict.Contains(handleStr))
        {
            return null;
        }

        var entityDictId = dict.GetAt(handleStr);
        var entityDict = (DBDictionary)tr.GetObject(entityDictId, OpenMode.ForRead);
        return GetXrecordValue(tr, entityDict, TemplateIdKey);
    }

    private ObjectId GetOrCreateBackupDictionary(Transaction tr, Database db)
    {
        // 获取命名对象字典
        var nod = (DBDictionary)tr.GetObject(db.NamedObjectsDictionaryId, OpenMode.ForRead);

        if (nod.Contains(BackupDictKey))
        {
            return nod.GetAt(BackupDictKey);
        }

        nod.UpgradeOpen();
        var dict = new DBDictionary();
        var dictId = nod.SetAt(BackupDictKey, dict);
        tr.AddNewlyCreatedDBObject(dict, true);
        return dictId;
    }

    private ObjectId GetBackupDictionary(Transaction tr, Database db)
    {
        var nod = (DBDictionary)tr.GetObject(db.NamedObjectsDictionaryId, OpenMode.ForRead);
        return nod.Contains(BackupDictKey) ? nod.GetAt(BackupDictKey) : ObjectId.Null;
    }

    private ObjectId GetOrCreateEntityDictionary(Transaction tr, DBDictionary parentDict, string key)
    {
        if (parentDict.Contains(key))
        {
            return parentDict.GetAt(key);
        }

        parentDict.UpgradeOpen();
        var dict = new DBDictionary();
        var dictId = parentDict.SetAt(key, dict);
        tr.AddNewlyCreatedDBObject(dict, true);
        return dictId;
    }

    private void SetXrecordValue(Transaction tr, DBDictionary dict, string key, string value)
    {
        var xrec = new Xrecord();
        xrec.Data = new ResultBuffer(new TypedValue((int)DxfCode.Text, value));

        if (dict.Contains(key))
        {
            var oldId = dict.GetAt(key);
            var oldRec = (Xrecord)tr.GetObject(oldId, OpenMode.ForWrite);
            oldRec.Dispose();
            dict.Remove(key);
        }

        dict.SetAt(key, xrec);
        tr.AddNewlyCreatedDBObject(xrec, true);
    }

    private string? GetXrecordValue(Transaction tr, DBDictionary dict, string key)
    {
        if (!dict.Contains(key)) return null;

        var xrecId = dict.GetAt(key);
        var xrec = (Xrecord)tr.GetObject(xrecId, OpenMode.ForRead);
        var rb = xrec.Data;

        if (rb == null) return null;

        var values = rb.AsArray();
        return values.Length > 0 ? values[0].Value?.ToString() : null;
    }

    private static string? NormalizeValue(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value;
}
