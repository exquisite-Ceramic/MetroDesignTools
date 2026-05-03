using Autodesk.AutoCAD.DatabaseServices;
using MetroToolKits.Foundation.Cad.Services;
using MetroToolKits.SectionGenerator.App.Abstractions;
using MetroToolKits.SectionGenerator.App.Models;

namespace MetroToolKits.SectionGenerator.Infrastructure.Services;

public sealed class CadConvertedElementMetadataInspector : IConvertedElementMetadataInspector
{
    private const string WallTypeId = "Wall";
    private const string DefaultWallTargetLayerPrefix = "MK_结构墙";

    private readonly IDocumentService _documentService;
    private readonly ElementConversionBackupService _backupService;
    private readonly IElementTypeCatalog _elementTypeCatalog;
    private readonly IWallAssemblyTemplateCatalog _wallTemplateCatalog;

    public CadConvertedElementMetadataInspector(
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

    public ConvertedElementMetadataInspectionResult InspectMissingWallTemplateMetadata()
    {
        var db = _documentService.GetCurrentDatabase();
        if (db == null)
        {
            return ConvertedElementMetadataInspectionResult.Empty;
        }

        using var tr = db.TransactionManager.StartTransaction();
        var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
        var modelSpace = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);

        var scannedEntityCount = 0;
        var wallCandidateCount = 0;
        var issueCandidates = new List<WallMetadataIssueCandidate>();
        var templateIdsByLayer = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        var wallTargetLayerPrefix = ResolveWallTargetLayerPrefix();

        foreach (var objectId in modelSpace)
        {
            scannedEntityCount++;
            if (tr.GetObject(objectId, OpenMode.ForRead, false) is not Entity entity)
            {
                continue;
            }

            if (!IsWallTargetLayer(entity.Layer, wallTargetLayerPrefix))
            {
                continue;
            }

            var hasBackup = _backupService.HasBackup(tr, entity);
            var currentConvertedType = hasBackup
                ? NormalizeMetadataValue(_backupService.GetConvertedType(tr, entity))
                : null;
            var currentTemplateId = hasBackup
                ? NormalizeMetadataValue(_backupService.GetTemplateId(tr, entity))
                : null;

            if (IsWallMetadata(currentConvertedType, currentTemplateId))
            {
                AddTemplateCandidate(templateIdsByLayer, entity.Layer, currentTemplateId!);
            }

            if (!IsSupportedWallCandidate(entity))
            {
                continue;
            }

            wallCandidateCount++;
            var reason = BuildIssueReason(hasBackup, currentConvertedType, currentTemplateId);
            if (reason == ConvertedElementMetadataIssueReason.None)
            {
                continue;
            }

            issueCandidates.Add(new WallMetadataIssueCandidate(
                entity.Handle.ToString(),
                entity.Layer,
                currentConvertedType,
                currentTemplateId,
                reason));
        }

        tr.Commit();

        var issues = issueCandidates
            .Select(candidate => CreateIssue(candidate, templateIdsByLayer))
            .ToList();

        return new ConvertedElementMetadataInspectionResult
        {
            ScannedEntityCount = scannedEntityCount,
            WallCandidateCount = wallCandidateCount,
            Issues = issues
        };
    }

    private ConvertedElementMetadataIssue CreateIssue(
        WallMetadataIssueCandidate candidate,
        IReadOnlyDictionary<string, HashSet<string>> templateIdsByLayer)
    {
        var reason = candidate.Reason;
        string? recommendedTemplateId = null;
        string? recommendedTemplateName = null;

        if (reason.HasFlag(ConvertedElementMetadataIssueReason.MissingTemplateId))
        {
            if (!templateIdsByLayer.TryGetValue(candidate.LayerName, out var layerTemplateIds) ||
                layerTemplateIds.Count == 0)
            {
                reason |= ConvertedElementMetadataIssueReason.NoTemplateCandidate;
            }
            else if (layerTemplateIds.Count > 1)
            {
                reason |= ConvertedElementMetadataIssueReason.AmbiguousTemplate;
            }
            else
            {
                recommendedTemplateId = layerTemplateIds.Single();
                recommendedTemplateName = _wallTemplateCatalog.GetById(recommendedTemplateId)?.TemplateName;
            }
        }

        return new ConvertedElementMetadataIssue
        {
            Handle = candidate.Handle,
            LayerName = candidate.LayerName,
            CurrentConvertedType = candidate.CurrentConvertedType,
            CurrentTemplateId = candidate.CurrentTemplateId,
            Reason = reason,
            RecommendedTemplateId = recommendedTemplateId,
            RecommendedTemplateName = recommendedTemplateName
        };
    }

    private static ConvertedElementMetadataIssueReason BuildIssueReason(
        bool hasBackup,
        string? currentConvertedType,
        string? currentTemplateId)
    {
        var reason = ConvertedElementMetadataIssueReason.None;
        if (!hasBackup)
        {
            reason |= ConvertedElementMetadataIssueReason.MissingBackupRecord;
        }

        if (string.IsNullOrWhiteSpace(currentConvertedType))
        {
            reason |= ConvertedElementMetadataIssueReason.MissingConvertedType;
        }

        if (string.IsNullOrWhiteSpace(currentTemplateId))
        {
            reason |= ConvertedElementMetadataIssueReason.MissingTemplateId;
        }

        return reason;
    }

    private static bool IsWallMetadata(string? convertedType, string? templateId)
        => string.Equals(convertedType, WallTypeId, StringComparison.OrdinalIgnoreCase) &&
           !string.IsNullOrWhiteSpace(templateId);

    private static void AddTemplateCandidate(
        IDictionary<string, HashSet<string>> templateIdsByLayer,
        string layerName,
        string templateId)
    {
        if (!templateIdsByLayer.TryGetValue(layerName, out var templateIds))
        {
            templateIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            templateIdsByLayer[layerName] = templateIds;
        }

        templateIds.Add(templateId);
    }

    private string ResolveWallTargetLayerPrefix()
        => _elementTypeCatalog.GetByTypeId(WallTypeId)?.TargetLayerPrefix ?? DefaultWallTargetLayerPrefix;

    private static bool IsWallTargetLayer(string? layerName, string wallTargetLayerPrefix)
        => !string.IsNullOrWhiteSpace(layerName) &&
           layerName.StartsWith(wallTargetLayerPrefix, StringComparison.OrdinalIgnoreCase);

    private static bool IsSupportedWallCandidate(Entity entity)
        => entity is Line ||
           entity is Polyline { Closed: false, NumberOfVertices: 2 };

    private static string? NormalizeMetadataValue(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value;

    private sealed record WallMetadataIssueCandidate(
        string Handle,
        string LayerName,
        string? CurrentConvertedType,
        string? CurrentTemplateId,
        ConvertedElementMetadataIssueReason Reason);
}
