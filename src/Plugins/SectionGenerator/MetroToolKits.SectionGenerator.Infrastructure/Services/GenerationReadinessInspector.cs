using Autodesk.AutoCAD.DatabaseServices;
using MetroToolKits.SectionGenerator.App.Abstractions;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;

namespace MetroToolKits.SectionGenerator.Infrastructure.Services;

/// <summary>
/// 当前图纸构件识别就绪度检查器。
/// </summary>
public sealed class GenerationReadinessInspector : IGenerationReadinessInspector
{
    private readonly ElementConversionBackupService _backupService;

    private static readonly Dictionary<string, string> LayerTypeMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Wall"] = "MK_结构墙",
        ["Column"] = "MK_结构柱",
        ["Slab"] = "MK_楼板"
    };

    public GenerationReadinessInspector(ElementConversionBackupService backupService)
    {
        _backupService = backupService;
    }

    public GenerationReadinessSummary Inspect()
    {
        var doc = Application.DocumentManager.MdiActiveDocument;
        if (doc == null)
        {
            return new GenerationReadinessSummary();
        }

        var recognizableWalls = 0;
        var recognizableColumns = 0;
        var recognizableSlabs = 0;
        var convertedCount = 0;
        var templatedWalls = 0;
        var templatedSlabs = 0;
        var legacyWalls = 0;
        var legacySlabs = 0;

        using var tr = doc.Database.TransactionManager.StartOpenCloseTransaction();
        var bt = (BlockTable)tr.GetObject(doc.Database.BlockTableId, OpenMode.ForRead);
        var modelSpace = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);

        foreach (var objectId in modelSpace)
        {
            if (tr.GetObject(objectId, OpenMode.ForRead) is not Entity entity)
            {
                continue;
            }

            var convertedType = _backupService.GetConvertedType(tr, entity);
            var templateId = _backupService.GetTemplateId(tr, entity);
            var elementType = convertedType ?? ResolveElementTypeFromLayer(entity.Layer);
            if (elementType == null)
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(convertedType))
            {
                convertedCount++;
            }

            switch (elementType)
            {
                case "Wall":
                    recognizableWalls++;
                    if (string.IsNullOrWhiteSpace(templateId))
                    {
                        legacyWalls++;
                    }
                    else
                    {
                        templatedWalls++;
                    }

                    break;

                case "Column":
                    recognizableColumns++;
                    break;

                case "Slab":
                    recognizableSlabs++;
                    if (string.IsNullOrWhiteSpace(templateId))
                    {
                        legacySlabs++;
                    }
                    else
                    {
                        templatedSlabs++;
                    }

                    break;
            }
        }

        tr.Commit();

        return new GenerationReadinessSummary
        {
            TotalRecognizableElementCount = recognizableWalls + recognizableColumns + recognizableSlabs,
            RecognizableWallCount = recognizableWalls,
            RecognizableColumnCount = recognizableColumns,
            RecognizableSlabCount = recognizableSlabs,
            ConvertedEntityCount = convertedCount,
            TemplatedWallCount = templatedWalls,
            TemplatedSlabCount = templatedSlabs,
            LegacyWallCount = legacyWalls,
            LegacySlabCount = legacySlabs
        };
    }

    private static string? ResolveElementTypeFromLayer(string layerName)
    {
        foreach (var mapping in LayerTypeMap)
        {
            if (layerName.StartsWith(mapping.Value, StringComparison.OrdinalIgnoreCase))
            {
                return mapping.Key;
            }
        }

        return null;
    }
}
