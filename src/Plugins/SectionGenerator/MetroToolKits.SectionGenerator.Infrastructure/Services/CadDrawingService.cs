using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using MetroToolKits.Foundation.Core.Diagnostics;
using MetroToolKits.Foundation.Core.Geometry;
using MetroToolKits.SectionGenerator.App.Abstractions;
using MetroToolKits.SectionGenerator.App.Diagnostics;
using MetroToolKits.SectionGenerator.App.Models;
using MetroToolKits.SectionGenerator.Core.Sections;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;

namespace MetroToolKits.SectionGenerator.Infrastructure.Services;

/// <summary>
/// AutoCAD 绘图服务 - 将剖面数据绘制为块并插入图纸
/// （原 CadAdapter 层，已合并至 Infrastructure）
/// </summary>
public sealed class CadDrawingService : IDrawingService
{
    private const string SectionSnapshotAppName = "MK_SectionSnapshot";
    private const string SectionSourceRefAppName = "MK_SectionSourceRef";
    private readonly CadHatchWriter _hatchWriter = new();

    // ── 单层 ──────────────────────────────────────────────────────────────────

    public DrawSectionBlockResult DrawSectionBlock(
        SectionGeometryData geometryData,
        Point3D insertionPoint,
        FloorConfig floorConfig,
        double geometryAnchorX = 0)
    {
        return DrawSectionBlock(
            geometryData,
            insertionPoint,
            floorConfig,
            new SectionOutputConfig(),
            geometryAnchorX);
    }

    public DrawSectionBlockResult DrawSectionBlock(
        SectionGeometryData geometryData,
        Point3D insertionPoint,
        FloorConfig floorConfig,
        SectionOutputConfig outputConfig,
        double geometryAnchorX = 0)
    {
        outputConfig = NormalizeOutputConfig(outputConfig);
        var singleFloorTotalHeight = geometryData.VerticalProfile == null
            ? floorConfig.Height + floorConfig.TopSlabThickness + floorConfig.BottomSlabThickness
            : (geometryData.VerticalProfile.GetBottomBoundaryTop(0) - geometryData.VerticalProfile.GetBottomBoundaryBottom(0))
              + floorConfig.Height
              + (geometryData.VerticalProfile.GetTopBoundaryTop(0) - geometryData.VerticalProfile.GetTopBoundaryBottom(0));

        var multiData = new MultiFloorSectionData
        {
            Floors      = new[] { geometryData },
            TotalHeight = singleFloorTotalHeight
        };
        return DrawMultiFloorSectionBlock(multiData, insertionPoint, new[] { floorConfig }, outputConfig, geometryAnchorX);
    }

    // ── 多楼层 ────────────────────────────────────────────────────────────────

    public DrawSectionBlockResult DrawMultiFloorSectionBlock(
        MultiFloorSectionData multiData,
        Point3D insertionPoint,
        IReadOnlyList<FloorConfig> floors,
        double geometryAnchorX = 0)
    {
        return DrawMultiFloorSectionBlock(
            multiData,
            insertionPoint,
            floors,
            new SectionOutputConfig(),
            geometryAnchorX);
    }

    public DrawSectionBlockResult DrawMultiFloorSectionBlock(
        MultiFloorSectionData multiData,
        Point3D insertionPoint,
        IReadOnlyList<FloorConfig> floors,
        SectionOutputConfig outputConfig,
        double geometryAnchorX = 0)
    {
        outputConfig = NormalizeOutputConfig(outputConfig);
        var doc = Application.DocumentManager.MdiActiveDocument;
        if (doc == null)
            throw CreateInfrastructureException("无活动文档，无法输出剖面块。");

        try
        {
            var db        = doc.Database;

            using var lockDoc = doc.LockDocument();
            using var tr      = db.TransactionManager.StartTransaction();

            EnsureLayers(tr, db, outputConfig);
            EnsureRegApp(tr, db, SectionSnapshotAppName);
            EnsureRegApp(tr, db, SectionSourceRefAppName);

            var bt       = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForWrite);
            var blockName = GenerateUniqueBlockName(bt, floors);
            var blockDef = new BlockTableRecord { Name = blockName, Origin = Point3d.Origin };
            bt.Add(blockDef);
            tr.AddNewlyCreatedDBObject(blockDef, true);
            var xOffset = ComputeDrawingOffset(multiData, geometryAnchorX);
            var hatchResults = new List<CadHatchDrawResult>();

            foreach (var floorData in multiData.Floors)
            {
                foreach (var element in floorData.Elements)
                {
                    var cutSegments = element.CutLineSegments.Count > 0
                        ? element.CutLineSegments
                        : element.CutLines.Select(line => new SectionLineSegment
                        {
                            Line = line,
                            Role = SectionLineRole.Generic
                        }).ToList();
                    foreach (var segment in cutSegments)
                    {
                        AddSourceAwareLine(
                            blockDef,
                            tr,
                            segment.Line,
                            ResolveCutLayer(outputConfig, segment.Role),
                            LineWeight.LineWeight050,
                            element,
                            xOffset);
                    }

                    foreach (var line in element.SightLines)
                    {
                        AddSourceAwareLine(
                            blockDef,
                            tr,
                            line,
                            SanitizeLayerName(outputConfig.LayerOptions.SightLineLayer, "MK_看线"),
                            LineWeight.LineWeight025,
                            element,
                            xOffset);
                    }

                    if (outputConfig.HatchOptions.Enabled)
                    {
                        foreach (var region in element.HatchRegions)
                        {
                            var result = _hatchWriter.TryWrite(blockDef, tr, region, outputConfig, xOffset);
                            hatchResults.Add(result);
                        }
                    }
                }

                var slabSegments = floorData.SlabLineSegments.Count > 0
                    ? floorData.SlabLineSegments
                    : floorData.SlabLines.Select(line => new SectionLineSegment
                    {
                        Line = line,
                        Role = SectionLineRole.Generic
                    }).ToList();
                foreach (var segment in slabSegments)
                {
                    AddLine(
                        blockDef,
                        tr,
                        segment.Line,
                        ResolveCutLayer(outputConfig, segment.Role),
                        LineWeight.LineWeight050,
                        xOffset);
                }

                if (outputConfig.HatchOptions.Enabled)
                {
                    foreach (var region in floorData.HatchRegions)
                    {
                        var result = _hatchWriter.TryWrite(blockDef, tr, region, outputConfig, xOffset);
                        hatchResults.Add(result);
                    }
                }
            }

            if (outputConfig.AnnotationOptions.GenerateAnnotations)
            {
                DrawFloorAnnotations(blockDef, tr, multiData, floors, outputConfig);
            }

            var ms       = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
            var blockRef = new BlockReference(
                new Point3d(insertionPoint.X, insertionPoint.Y, insertionPoint.Z),
                blockDef.ObjectId);
            ms.AppendEntity(blockRef);
            tr.AddNewlyCreatedDBObject(blockRef, true);

            AttachSnapshotXData(blockRef, tr, multiData, db);

            tr.Commit();
            return new DrawSectionBlockResult
            {
                BlockName = blockName,
                BlockHandle = blockRef.Handle.ToString(),
                HatchSummary = BuildHatchSummary(hatchResults)
            };
        }
        catch (InfrastructureException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw CreateInfrastructureException($"剖面绘制失败: {ex.Message}", ex);
        }
    }

    // ── 标注 ──────────────────────────────────────────────────────────────────

    private static void DrawFloorAnnotations(BlockTableRecord btr, Transaction tr,
        MultiFloorSectionData multiData, IReadOnlyList<FloorConfig> floors, SectionOutputConfig outputConfig)
    {
        const double dimOffsetX = -800;
        var annotationLayer = SanitizeLayerName(outputConfig.LayerOptions.AnnotationLayer, "MK_标注");

        for (int i = 0; i < multiData.Floors.Count; i++)
        {
            var floorData = multiData.Floors[i];
            var config    = floors[i];
            double baseZ  = floorData.BaseElevation;
            double topZ   = baseZ + config.Height;

            var dim = new AlignedDimension(
                new Point3d(dimOffsetX, baseZ, 0),
                new Point3d(dimOffsetX, topZ, 0),
                new Point3d(dimOffsetX - 300, (baseZ + topZ) / 2, 0),
                string.Empty, ObjectId.Null)
            {
                Layer = annotationLayer
            };
            btr.AppendEntity(dim);
            tr.AddNewlyCreatedDBObject(dim, true);

            AddText(btr, tr, new Point3d(dimOffsetX - 600, (baseZ + topZ) / 2, 0), config.Name, 150, annotationLayer);

            double elevM = baseZ / 1000.0;
            AddText(btr, tr, new Point3d(200, baseZ, 0),
                elevM >= 0 ? $"+{elevM:F3}" : $"{elevM:F3}", 120, annotationLayer);

            if (config.TopBoundarySlab.SlopeEnabled && config.TopBoundarySlab.SlopeValue > 0)
                AddText(btr, tr,
                    new Point3d(0, topZ + config.TopSlabThickness + 200, 0),
                    $"顶板 i={config.TopBoundarySlab.SlopeValue * 100:F1}%", 100, annotationLayer);

            if (config.BottomBoundarySlab.SlopeEnabled && config.BottomBoundarySlab.SlopeValue > 0)
                AddText(btr, tr,
                    new Point3d(0, baseZ - config.BottomSlabThickness - 200, 0),
                    $"底板 i={config.BottomBoundarySlab.SlopeValue * 100:F1}%", 100, annotationLayer);
        }

        if (multiData.Floors.Count > 1)
        {
            double totalH = multiData.TotalHeight;
            var totalDim  = new AlignedDimension(
                new Point3d(dimOffsetX - 600, 0, 0),
                new Point3d(dimOffsetX - 600, totalH, 0),
                new Point3d(dimOffsetX - 900, totalH / 2, 0),
                string.Empty, ObjectId.Null)
            {
                Layer = annotationLayer
            };
            btr.AppendEntity(totalDim);
            tr.AddNewlyCreatedDBObject(totalDim, true);
        }
    }

    // ── 辅助 ──────────────────────────────────────────────────────────────────

    private static void EnsureLayers(Transaction tr, Database db, SectionOutputConfig outputConfig)
    {
        var lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
        var layerOptions = outputConfig.LayerOptions;
        foreach (var name in new[]
        {
            SanitizeLayerName(layerOptions.CutLineLayer, "MK_剖切线"),
            SanitizeLayerName(layerOptions.SightLineLayer, "MK_看线"),
            SanitizeLayerName(layerOptions.AnnotationLayer, "MK_标注"),
            SanitizeLayerName(layerOptions.WallHatchLayer, "MK_墙填充"),
            SanitizeLayerName(layerOptions.ColumnHatchLayer, "MK_柱填充"),
            SanitizeLayerName(layerOptions.SlabHatchLayer, "MK_楼板填充"),
            SanitizeLayerName(layerOptions.StructuralLayer, "MK_结构输出"),
            SanitizeLayerName(layerOptions.FinishLayer, "MK_装修输出")
        }.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (lt.Has(name)) continue;
            lt.UpgradeOpen();
            var layer = new LayerTableRecord { Name = name };
            lt.Add(layer);
            tr.AddNewlyCreatedDBObject(layer, true);
        }
    }

    private static string GenerateUniqueBlockName(BlockTable blockTable, IReadOnlyList<FloorConfig> floors)
    {
        var floorPart = string.Join("_", floors.Select(f => f.Name));
        var baseName = $"MK_剖面_{floorPart}_{DateTime.Now:yyyyMMddTHHmmssfff}";
        var candidate = baseName;
        var suffix = 1;

        while (blockTable.Has(candidate))
        {
            candidate = $"{baseName}_{suffix}";
            suffix++;
        }

        return candidate;
    }

    private static void AddLine(BlockTableRecord btr, Transaction tr,
        Line3D line, string layer, LineWeight lineWeight, double xOffset)
    {
        var cadLine = new Line(
            new Point3d(line.Start.X + xOffset, line.Start.Y, 0),
            new Point3d(line.End.X + xOffset,   line.End.Y,   0))
        { Layer = layer, LineWeight = lineWeight };
        btr.AppendEntity(cadLine);
        tr.AddNewlyCreatedDBObject(cadLine, true);
    }

    private static void AddSourceAwareLine(BlockTableRecord btr, Transaction tr,
        Line3D line, string layer, LineWeight lineWeight, ElementSectionData element, double xOffset)
    {
        var cadLine = new Line(
            new Point3d(line.Start.X + xOffset, line.Start.Y, 0),
            new Point3d(line.End.X + xOffset, line.End.Y, 0))
        {
            Layer = layer,
            LineWeight = lineWeight
        };

        btr.AppendEntity(cadLine);
        tr.AddNewlyCreatedDBObject(cadLine, true);

        if (!string.IsNullOrWhiteSpace(element.SourceHandle))
        {
            cadLine.XData = new ResultBuffer(
                new TypedValue((int)DxfCode.ExtendedDataRegAppName, SectionSourceRefAppName),
                new TypedValue((int)DxfCode.ExtendedDataAsciiString, element.SourceHandle),
                new TypedValue((int)DxfCode.ExtendedDataAsciiString, element.ElementType));
        }
    }

    private static void AddText(BlockTableRecord btr, Transaction tr,
        Point3d position, string content, double height, string layer)
    {
        var text = new DBText
        { Position = position, TextString = content, Height = height, Layer = layer };
        btr.AppendEntity(text);
        tr.AddNewlyCreatedDBObject(text, true);
    }

    private static void AttachSnapshotXData(BlockReference blockRef, Transaction tr,
        MultiFloorSectionData data, Database db)
    {
        var floorNames = string.Join(",", data.Floors.Select(f => f.FloorName));
        blockRef.XData = new ResultBuffer(
            new TypedValue((int)DxfCode.ExtendedDataRegAppName, SectionSnapshotAppName),
            new TypedValue((int)DxfCode.ExtendedDataAsciiString, floorNames),
            new TypedValue((int)DxfCode.ExtendedDataAsciiString, DateTime.Now.ToString("O")),
            new TypedValue((int)DxfCode.ExtendedDataReal, data.TotalHeight));
    }

    private static void EnsureRegApp(Transaction tr, Database db, string appName)
    {
        var rat = (RegAppTable)tr.GetObject(db.RegAppTableId, OpenMode.ForRead);
        if (rat.Has(appName)) return;

        rat.UpgradeOpen();
        var rec = new RegAppTableRecord { Name = appName };
        rat.Add(rec);
        tr.AddNewlyCreatedDBObject(rec, true);
    }

    private static InfrastructureException CreateInfrastructureException(string technicalMessage, Exception? innerException = null)
    {
        return new InfrastructureException(SectionGenerationFailures.DrawFailed(technicalMessage, innerException));
    }

    private static HatchOutputSummary BuildHatchSummary(IReadOnlyList<CadHatchDrawResult> results)
    {
        if (results.Count == 0)
        {
            return HatchOutputSummary.Empty;
        }

        var warnings = results
            .Where(result => !result.Succeeded || IsPatternFallback(result))
            .Select(ToHatchOutputWarning)
            .ToList();

        return new HatchOutputSummary
        {
            RequestedCount = results.Count,
            CreatedCount = results.Count(result => result.Succeeded),
            SkippedCount = results.Count(result => !result.Succeeded),
            FallbackPatternCount = results.Count(IsPatternFallback),
            Warnings = warnings
        };
    }

    private static HatchOutputWarning ToHatchOutputWarning(CadHatchDrawResult result)
    {
        var code = ResolveHatchWarningCode(result);
        return new HatchOutputWarning
        {
            Category = result.Category,
            Code = code,
            Message = BuildHatchWarningMessage(result, code),
            PatternName = result.PatternName,
            EffectivePatternName = result.EffectivePatternName,
            LayerName = result.LayerName,
            BoundaryPointCount = result.BoundaryPointCount
        };
    }

    private static string ResolveHatchWarningCode(CadHatchDrawResult result)
    {
        if (IsPatternFallback(result))
        {
            return SectionGenerationErrorCodes.HatchPatternFallback;
        }

        var message = result.Message ?? string.Empty;
        if (message.Contains("Invalid hatch boundary", StringComparison.OrdinalIgnoreCase))
        {
            return SectionGenerationErrorCodes.HatchBoundaryInvalid;
        }

        if (message.Contains("Evaluate", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("eInvalidInput", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("Hatch creation failed", StringComparison.OrdinalIgnoreCase))
        {
            return SectionGenerationErrorCodes.HatchEvaluateFailed;
        }

        return SectionGenerationErrorCodes.HatchSkipped;
    }

    private static string BuildHatchWarningMessage(CadHatchDrawResult result, string code)
    {
        if (code == SectionGenerationErrorCodes.HatchPatternFallback)
        {
            return $"填充图案 {result.PatternName} 不可用，已回退到 {result.EffectivePatternName}。";
        }

        var detail = string.IsNullOrWhiteSpace(result.Message)
            ? "未提供详细错误。"
            : result.Message;

        return code switch
        {
            SectionGenerationErrorCodes.HatchBoundaryInvalid =>
                $"跳过非法填充区域。{detail}",
            SectionGenerationErrorCodes.HatchEvaluateFailed =>
                $"填充输出失败，已跳过该填充区域。{detail}",
            _ =>
                $"已跳过填充区域。{detail}"
        };
    }

    private static bool IsPatternFallback(CadHatchDrawResult result)
        => result.Succeeded &&
           !string.IsNullOrWhiteSpace(result.PatternName) &&
           !string.IsNullOrWhiteSpace(result.EffectivePatternName) &&
           !string.Equals(result.PatternName, result.EffectivePatternName, StringComparison.OrdinalIgnoreCase);

    private static string ResolveCutLayer(SectionOutputConfig outputConfig, SectionLineRole role)
    {
        return role switch
        {
            SectionLineRole.Structural => SanitizeLayerName(outputConfig.LayerOptions.StructuralLayer, "MK_结构输出"),
            SectionLineRole.Finish => SanitizeLayerName(outputConfig.LayerOptions.FinishLayer, "MK_装修输出"),
            _ => SanitizeLayerName(outputConfig.LayerOptions.CutLineLayer, "MK_剖切线")
        };
    }

    private static string SanitizeLayerName(string? configuredName, string fallback)
        => string.IsNullOrWhiteSpace(configuredName) ? fallback : configuredName.Trim();

    private static SectionOutputConfig NormalizeOutputConfig(SectionOutputConfig? outputConfig)
    {
        outputConfig ??= new SectionOutputConfig();
        outputConfig.AnnotationOptions ??= new AnnotationOptions();
        outputConfig.HatchOptions ??= new HatchOptions();
        outputConfig.HatchOptions.WallHatch ??= HatchStyleOptions.CreateDefault();
        outputConfig.HatchOptions.ColumnHatch ??= HatchStyleOptions.CreateDefault();
        outputConfig.HatchOptions.SlabHatch ??= HatchStyleOptions.CreateDefault();
        outputConfig.LayerOptions ??= new LayerOptions();
        return outputConfig;
    }

    private static double ComputeDrawingOffset(MultiFloorSectionData multiData, double geometryAnchorX)
    {
        var xValues = multiData.AllCutLines
            .Concat(multiData.AllSightLines)
            .SelectMany(line => new[] { line.Start.X, line.End.X })
            .ToList();

        if (xValues.Count == 0)
        {
            return geometryAnchorX;
        }

        var currentMinX = xValues.Min();
        return geometryAnchorX - currentMinX;
    }
}
