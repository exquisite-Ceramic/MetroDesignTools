using Autodesk.AutoCAD.DatabaseServices;
using MetroToolKits.Foundation.Cad.Services;
using MetroToolKits.SectionGenerator.App.Abstractions;
using MetroToolKits.SectionGenerator.App.Models;

namespace MetroToolKits.SectionGenerator.Infrastructure.Services;

public sealed class CadConvertedElementMetadataRepairService : IConvertedElementMetadataRepairService
{
    private const string WallTypeId = "Wall";
    private const string DefaultWallTargetLayerPrefix = "MK_结构墙";

    private readonly IDocumentService _documentService;
    private readonly ElementConversionBackupService _backupService;
    private readonly IElementTypeCatalog _elementTypeCatalog;
    private readonly IWallAssemblyTemplateCatalog _wallTemplateCatalog;

    public CadConvertedElementMetadataRepairService(
        IDocumentService documentService,
        ElementConversionBackupService backupService,
        IElementTypeCatalog elementTypeCatalog,
        IWallAssemblyTemplateCatalog wallTemplateCatalog)
    {
        _documentService = documentService;
        _backupService = backupService;
        _elementTypeCatalog = elementTypeCatalog;
        _wallTemplateCatalog = wallTemplateCatalog;
    }

    public ConvertedElementMetadataRepairResult RepairMissingWallTemplateMetadata(
        ConvertedElementMetadataRepairRequest request)
    {
        var failures = new List<ConvertedElementMetadataRepairFailure>();
        var warnings = new List<string>();
        var handles = request.Handles ?? Array.Empty<string>();
        var requestedCount = handles.Count;
        var repairedCount = 0;

        if (!string.Equals(request.ConvertedType, WallTypeId, StringComparison.OrdinalIgnoreCase))
        {
            return FailAll(
                handles,
                "InvalidConvertedType",
                $"当前修复服务只支持 ConvertedType={WallTypeId}。",
                warnings);
        }

        if (string.IsNullOrWhiteSpace(request.TemplateId) ||
            _wallTemplateCatalog.GetById(request.TemplateId) == null)
        {
            return FailAll(
                handles,
                "InvalidTemplateId",
                "TemplateId 为空或不存在于墙体模板目录。",
                warnings);
        }

        var db = _documentService.GetCurrentDatabase();
        if (db == null)
        {
            return FailAll(handles, "NoActiveDatabase", "当前没有可写入的 AutoCAD 数据库。", warnings);
        }

        var wallTargetLayerPrefix = ResolveWallTargetLayerPrefix();
        using var lockDoc = _documentService.LockDocument();
        using var tr = db.TransactionManager.StartTransaction();

        foreach (var handle in handles)
        {
            var entity = GetEntity(tr, db, handle);
            if (entity == null)
            {
                failures.Add(CreateFailure(handle, null, "EntityNotFound", "未找到指定 handle 对应的实体。"));
                continue;
            }

            if (!IsSupportedWallCandidate(entity))
            {
                failures.Add(CreateFailure(
                    handle,
                    entity.Layer,
                    "UnsupportedEntityType",
                    $"实体类型 {entity.GetType().Name} 不是当前支持的墙体图元。"));
                continue;
            }

            if (!IsWallTargetLayer(entity.Layer, wallTargetLayerPrefix))
            {
                failures.Add(CreateFailure(
                    handle,
                    entity.Layer,
                    "NotWallTargetLayer",
                    $"实体不在 {wallTargetLayerPrefix}_* 图层上。"));
                continue;
            }

            var inferredOriginalLayer = InferOriginalLayer(entity.Layer, wallTargetLayerPrefix, out var warning);
            if (!string.IsNullOrWhiteSpace(warning))
            {
                warnings.Add($"Handle={handle}: {warning}");
            }

            try
            {
                if (!_backupService.BackfillConversionMetadata(
                        tr,
                        entity,
                        WallTypeId,
                        request.TemplateId,
                        inferredOriginalLayer,
                        request.OverwriteTemplateId,
                        out var failureMessage))
                {
                    failures.Add(CreateFailure(
                        handle,
                        entity.Layer,
                        "BackfillFailed",
                        failureMessage ?? "转换元数据补写失败。"));
                    continue;
                }

                repairedCount++;
            }
            catch (Exception ex)
            {
                failures.Add(CreateFailure(
                    handle,
                    entity.Layer,
                    "XrecordWriteFailed",
                    $"写入转换元数据失败: {ex.Message}"));
            }
        }

        tr.Commit();
        return new ConvertedElementMetadataRepairResult
        {
            RequestedCount = requestedCount,
            RepairedCount = repairedCount,
            FailedCount = failures.Count,
            Failures = failures,
            Warnings = warnings
        };
    }

    private static ConvertedElementMetadataRepairResult FailAll(
        IReadOnlyList<string> handles,
        string code,
        string message,
        IReadOnlyList<string> warnings)
    {
        return new ConvertedElementMetadataRepairResult
        {
            RequestedCount = handles.Count,
            FailedCount = handles.Count,
            Failures = handles
                .Select(handle => CreateFailure(handle, null, code, message))
                .ToList(),
            Warnings = warnings
        };
    }

    private static ConvertedElementMetadataRepairFailure CreateFailure(
        string handle,
        string? layerName,
        string code,
        string message)
    {
        return new ConvertedElementMetadataRepairFailure
        {
            Handle = handle,
            LayerName = layerName,
            Code = code,
            Message = message
        };
    }

    private string ResolveWallTargetLayerPrefix()
        => _elementTypeCatalog.GetByTypeId(WallTypeId)?.TargetLayerPrefix ?? DefaultWallTargetLayerPrefix;

    private static string? InferOriginalLayer(
        string layerName,
        string wallTargetLayerPrefix,
        out string? warning)
    {
        warning = null;
        var expectedPrefix = wallTargetLayerPrefix + "_";
        if (!layerName.StartsWith(expectedPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var inferred = layerName[expectedPrefix.Length..];
        if (string.IsNullOrWhiteSpace(inferred))
        {
            warning = "无法从目标图层名推断 OriginalLayer，还原可能受限。";
            return null;
        }

        if (inferred.StartsWith(expectedPrefix, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(inferred, wallTargetLayerPrefix, StringComparison.OrdinalIgnoreCase))
        {
            warning = "目标图层像重复转换图层，未补写 OriginalLayer，还原可能受限。";
            return null;
        }

        return inferred;
    }

    private static bool IsWallTargetLayer(string? layerName, string wallTargetLayerPrefix)
        => !string.IsNullOrWhiteSpace(layerName) &&
           layerName.StartsWith(wallTargetLayerPrefix + "_", StringComparison.OrdinalIgnoreCase);

    private static bool IsSupportedWallCandidate(Entity entity)
        => entity is Line ||
           entity is Polyline { Closed: false, NumberOfVertices: 2 };

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
