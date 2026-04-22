using Autodesk.AutoCAD.DatabaseServices;
using MetroToolKits.Foundation.Cad.Services;
using MetroToolKits.SectionGenerator.App.Abstractions;

namespace MetroToolKits.SectionGenerator.Infrastructure.Services;

/// <summary>
/// 基于 AutoCAD 数据库的构件转换实现。
/// </summary>
public sealed class CadElementConversionService : IElementConversionService
{
    private readonly IDocumentService _documentService;
    private readonly ILayerService _layerService;
    private readonly ElementConversionBackupService _backupService;

    public CadElementConversionService(
        IDocumentService documentService,
        ILayerService layerService,
        ElementConversionBackupService backupService)
    {
        _documentService = documentService;
        _layerService = layerService;
        _backupService = backupService;
    }

    public IReadOnlyList<string> GetDistinctLayers(IReadOnlyCollection<string> entityHandles)
    {
        var db = _documentService.GetCurrentDatabase();
        if (db == null || entityHandles.Count == 0)
        {
            return Array.Empty<string>();
        }

        using var tr = db.TransactionManager.StartTransaction();
        var layers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var handle in entityHandles)
        {
            var entity = GetEntity(tr, db, handle);
            if (entity != null && !string.IsNullOrWhiteSpace(entity.Layer))
            {
                layers.Add(entity.Layer);
            }
        }

        tr.Abort();
        return layers.OrderBy(static layer => layer, StringComparer.OrdinalIgnoreCase).ToList();
    }

    public ElementConversionApplySummary ApplyMappingsToSelection(
        IReadOnlyCollection<string> entityHandles,
        IReadOnlyCollection<ElementLayerMapping> mappings)
    {
        var db = _documentService.GetCurrentDatabase();
        if (db == null || entityHandles.Count == 0 || mappings.Count == 0)
        {
            return new ElementConversionApplySummary();
        }

        var mappingLookup = mappings.ToDictionary(
            mapping => mapping.SourceLayerName,
            mapping => mapping,
            StringComparer.OrdinalIgnoreCase);

        using var lockDoc = _documentService.LockDocument();
        foreach (var mapping in mappings)
        {
            var targetLayer = BuildTargetLayerName(mapping);
            _layerService.GetOrCreateLayer(targetLayer);
            _layerService.SetLayerColor(targetLayer, mapping.ElementType.LayerColorIndex);
        }

        using var tr = db.TransactionManager.StartTransaction();
        var convertedCount = 0;
        foreach (var handle in entityHandles)
        {
            var entity = GetEntity(tr, db, handle);
            if (entity == null || !mappingLookup.TryGetValue(entity.Layer, out var mapping))
            {
                continue;
            }

            entity.UpgradeOpen();
            _backupService.BackupEntity(tr, entity, mapping.ElementType.TypeId, mapping.TemplateId);
            entity.Layer = BuildTargetLayerName(mapping);
            convertedCount++;
        }

        tr.Commit();
        return new ElementConversionApplySummary
        {
            ConvertedCount = convertedCount,
            MappedLayerCount = mappingLookup.Count
        };
    }

    public ElementConversionApplySummary ApplyMappingsToEntireDrawing(
        IReadOnlyCollection<ElementLayerMapping> mappings)
    {
        var db = _documentService.GetCurrentDatabase();
        if (db == null || mappings.Count == 0)
        {
            return new ElementConversionApplySummary();
        }

        var mappingLookup = mappings.ToDictionary(
            mapping => mapping.SourceLayerName,
            mapping => mapping,
            StringComparer.OrdinalIgnoreCase);

        using var lockDoc = _documentService.LockDocument();
        foreach (var mapping in mappings)
        {
            var targetLayer = BuildTargetLayerName(mapping);
            _layerService.GetOrCreateLayer(targetLayer);
            _layerService.SetLayerColor(targetLayer, mapping.ElementType.LayerColorIndex);
        }

        using var tr = db.TransactionManager.StartTransaction();
        var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
        var btr = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);

        var convertedCount = 0;
        foreach (var objectId in btr)
        {
            var entity = tr.GetObject(objectId, OpenMode.ForRead) as Entity;
            if (entity == null || !mappingLookup.TryGetValue(entity.Layer, out var mapping))
            {
                continue;
            }

            entity.UpgradeOpen();
            _backupService.BackupEntity(tr, entity, mapping.ElementType.TypeId, mapping.TemplateId);
            entity.Layer = BuildTargetLayerName(mapping);
            convertedCount++;
        }

        tr.Commit();
        return new ElementConversionApplySummary
        {
            ConvertedCount = convertedCount,
            MappedLayerCount = mappingLookup.Count
        };
    }

    public ElementConversionRevertSummary Revert(IReadOnlyCollection<string> entityHandles)
    {
        var db = _documentService.GetCurrentDatabase();
        if (db == null || entityHandles.Count == 0)
        {
            return new ElementConversionRevertSummary();
        }

        using var lockDoc = _documentService.LockDocument();
        using var tr = db.TransactionManager.StartTransaction();

        var restoredCount = 0;
        var skippedCount = 0;
        foreach (var handle in entityHandles)
        {
            var entity = GetEntity(tr, db, handle);
            if (entity == null)
            {
                skippedCount++;
                continue;
            }

            if (_backupService.HasBackup(tr, entity) && _backupService.RestoreEntity(tr, entity))
            {
                restoredCount++;
            }
            else
            {
                skippedCount++;
            }
        }

        tr.Commit();
        return new ElementConversionRevertSummary
        {
            RestoredCount = restoredCount,
            SkippedCount = skippedCount
        };
    }

    public int RevertAll()
    {
        var db = _documentService.GetCurrentDatabase();
        if (db == null)
        {
            return 0;
        }

        using var lockDoc = _documentService.LockDocument();
        using var tr = db.TransactionManager.StartTransaction();

        var restoredCount = 0;
        var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
        var btr = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);

        foreach (var objectId in btr)
        {
            var entity = tr.GetObject(objectId, OpenMode.ForRead) as Entity;
            if (entity != null && _backupService.HasBackup(tr, entity) && _backupService.RestoreEntity(tr, entity))
            {
                restoredCount++;
            }
        }

        tr.Commit();
        return restoredCount;
    }

    private static string BuildTargetLayerName(ElementLayerMapping mapping)
        => $"{mapping.ElementType.TargetLayerPrefix}_{mapping.SourceLayerName}";

    private static Entity? GetEntity(Transaction tr, Database db, string handle)
    {
        if (!TryGetObjectId(db, handle, out var objectId))
        {
            return null;
        }

        return tr.GetObject(objectId, OpenMode.ForRead, false) as Entity;
    }

    private static bool TryGetObjectId(Database db, string handle, out ObjectId objectId)
    {
        objectId = ObjectId.Null;
        if (string.IsNullOrWhiteSpace(handle))
        {
            return false;
        }

        try
        {
            var value = Convert.ToInt64(handle, 16);
            objectId = db.GetObjectId(false, new Handle(value), 0);
            return !objectId.IsNull;
        }
        catch
        {
            return false;
        }
    }
}
