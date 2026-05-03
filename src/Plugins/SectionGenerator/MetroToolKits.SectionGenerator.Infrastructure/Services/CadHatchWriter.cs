using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using MetroToolKits.Foundation.Core.Geometry;
using MetroToolKits.SectionGenerator.App.Models;
using MetroToolKits.SectionGenerator.Core.Sections;
using MetroToolKits.SectionGenerator.Core.Sections.Hatching;
using AcadException = Autodesk.AutoCAD.Runtime.Exception;

namespace MetroToolKits.SectionGenerator.Infrastructure.Services;

public sealed class CadHatchWriter
{
    private const string DefaultPatternName = "ANSI31";
    private const string SolidPatternName = "SOLID";

    public CadHatchDrawResult TryWrite(
        BlockTableRecord btr,
        Transaction tr,
        SectionHatchRegion region,
        SectionOutputConfig outputConfig,
        double xOffset)
    {
        if (region is null)
        {
            return Failure(
                default,
                null,
                null,
                null,
                "Hatch region is null.",
                null);
        }

        outputConfig ??= new SectionOutputConfig();
        var boundaryPoints = region.Boundary ?? Array.Empty<Point3D>();
        var boundaryPointCount = boundaryPoints.Count;
        var style = ResolveHatchStyle(outputConfig, region.Category);
        var layer = ResolveHatchLayer(outputConfig, region.Category);
        var patternName = ResolvePatternName(style);

        if (btr is null)
        {
            return Failure(
                region.Category,
                patternName,
                null,
                layer,
                "Target block table record is null.",
                null,
                boundaryPointCount);
        }

        if (tr is null)
        {
            return Failure(
                region.Category,
                patternName,
                null,
                layer,
                "Transaction is null.",
                null,
                boundaryPointCount);
        }

        var validation = HatchBoundaryNormalizer.Normalize(boundaryPoints);
        if (!validation.IsValid)
        {
            return Failure(
                region.Category,
                patternName,
                null,
                layer,
                $"Invalid hatch boundary: {validation.IssueCode}. {validation.Message}",
                null,
                boundaryPointCount);
        }

        var outermostResult = TryWriteWithLoopType(
            btr,
            tr,
            region,
            style,
            patternName,
            layer,
            validation.NormalizedBoundary,
            boundaryPointCount,
            xOffset,
            HatchLoopTypes.Outermost);
        if (outermostResult.Succeeded)
        {
            return outermostResult;
        }

        var externalResult = TryWriteWithLoopType(
            btr,
            tr,
            region,
            style,
            patternName,
            layer,
            validation.NormalizedBoundary,
            boundaryPointCount,
            xOffset,
            HatchLoopTypes.External);
        if (externalResult.Succeeded)
        {
            return new CadHatchDrawResult
            {
                Succeeded = true,
                Category = externalResult.Category,
                PatternName = externalResult.PatternName,
                EffectivePatternName = externalResult.EffectivePatternName,
                LayerName = externalResult.LayerName,
                BoundaryPointCount = externalResult.BoundaryPointCount,
                Message = $"Hatch created with {HatchLoopTypes.External} loop type after {HatchLoopTypes.Outermost} failed. Outermost failure: {outermostResult.Message}"
            };
        }

        return Failure(
            region.Category,
            patternName,
            externalResult.EffectivePatternName ?? outermostResult.EffectivePatternName,
            layer,
            $"Hatch creation failed with both loop types. {HatchLoopTypes.Outermost}: {outermostResult.Message} {HatchLoopTypes.External}: {externalResult.Message}",
            externalResult.Exception ?? outermostResult.Exception,
            boundaryPointCount);
    }

    private static CadHatchDrawResult TryWriteWithLoopType(
        BlockTableRecord btr,
        Transaction tr,
        SectionHatchRegion region,
        HatchStyleOptions style,
        string patternName,
        string layer,
        IReadOnlyList<Point3D> normalizedBoundary,
        int boundaryPointCount,
        double xOffset,
        HatchLoopTypes loopType)
    {
        Polyline? boundary = null;
        Hatch? hatch = null;
        string? effectivePatternName = null;
        try
        {
            boundary = CreateBoundary(normalizedBoundary, layer, xOffset);
            btr.AppendEntity(boundary);
            tr.AddNewlyCreatedDBObject(boundary, true);

            hatch = new Hatch
            {
                Layer = layer
            };
            btr.AppendEntity(hatch);
            tr.AddNewlyCreatedDBObject(hatch, true);

            if (!TrySetPattern(hatch, patternName, out effectivePatternName, out var patternFailure))
            {
                TryErase(hatch);
                TryErase(boundary);
                return Failure(
                    region.Category,
                    patternName,
                    null,
                    layer,
                    $"Unable to set hatch pattern '{patternName}', '{DefaultPatternName}' or '{SolidPatternName}'. {FormatException(patternFailure)}",
                    patternFailure,
                    boundaryPointCount);
            }

            hatch.PatternScale = style.Scale <= 0 ? 100.0 : style.Scale;
            hatch.PatternAngle = style.Angle * Math.PI / 180.0;
            hatch.Associative = true;
            hatch.AppendLoop(
                loopType,
                new ObjectIdCollection(new[] { boundary.ObjectId }));
            hatch.EvaluateHatch(true);

            return new CadHatchDrawResult
            {
                Succeeded = true,
                Category = region.Category,
                PatternName = patternName,
                EffectivePatternName = effectivePatternName,
                LayerName = layer,
                BoundaryPointCount = boundaryPointCount,
                Message = $"Hatch created with {loopType} loop type."
            };
        }
        catch (Exception ex)
        {
            TryErase(hatch);
            TryErase(boundary);
            return Failure(
                region.Category,
                patternName,
                effectivePatternName,
                layer,
                $"Hatch creation failed with {loopType} loop type. {FormatException(ex)}",
                ex,
                boundaryPointCount);
        }
    }

    private static Polyline CreateBoundary(
        IReadOnlyList<Point3D> points,
        string layer,
        double xOffset)
    {
        var boundary = new Polyline();
        for (var i = 0; i < points.Count; i++)
        {
            boundary.AddVertexAt(
                i,
                new Point2d(points[i].X + xOffset, points[i].Y),
                0,
                0,
                0);
        }

        boundary.Closed = true;
        boundary.Layer = layer;
        boundary.Visible = false;
        return boundary;
    }

    private static bool TrySetPattern(
        Hatch hatch,
        string patternName,
        out string? effectivePatternName,
        out Exception? exception)
    {
        effectivePatternName = null;
        exception = null;

        foreach (var candidate in GetPatternCandidates(patternName))
        {
            try
            {
                hatch.SetHatchPattern(HatchPatternType.PreDefined, candidate);
                effectivePatternName = candidate;
                return true;
            }
            catch (Exception ex)
            {
                exception = ex;
            }
        }

        return false;
    }

    private static IEnumerable<string> GetPatternCandidates(string patternName)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var candidate in new[] { patternName, DefaultPatternName, SolidPatternName })
        {
            if (!string.IsNullOrWhiteSpace(candidate) && seen.Add(candidate.Trim()))
            {
                yield return candidate.Trim();
            }
        }
    }

    private static HatchStyleOptions ResolveHatchStyle(SectionOutputConfig outputConfig, SectionHatchCategory category)
    {
        outputConfig.HatchOptions ??= new HatchOptions();
        outputConfig.HatchOptions.WallHatch ??= HatchStyleOptions.CreateDefault();
        outputConfig.HatchOptions.ColumnHatch ??= HatchStyleOptions.CreateDefault();
        outputConfig.HatchOptions.SlabHatch ??= HatchStyleOptions.CreateDefault();

        return category switch
        {
            SectionHatchCategory.Wall => outputConfig.HatchOptions.WallHatch,
            SectionHatchCategory.Column => outputConfig.HatchOptions.ColumnHatch,
            SectionHatchCategory.Slab => outputConfig.HatchOptions.SlabHatch,
            _ => HatchStyleOptions.CreateDefault()
        };
    }

    private static string ResolvePatternName(HatchStyleOptions style)
        => string.IsNullOrWhiteSpace(style.PatternName) ? DefaultPatternName : style.PatternName.Trim();

    private static string ResolveHatchLayer(SectionOutputConfig outputConfig, SectionHatchCategory category)
    {
        outputConfig.LayerOptions ??= new LayerOptions();
        return category switch
        {
            SectionHatchCategory.Wall => SanitizeLayerName(outputConfig.LayerOptions.WallHatchLayer, "MK_墙填充"),
            SectionHatchCategory.Column => SanitizeLayerName(outputConfig.LayerOptions.ColumnHatchLayer, "MK_柱填充"),
            SectionHatchCategory.Slab => SanitizeLayerName(outputConfig.LayerOptions.SlabHatchLayer, "MK_楼板填充"),
            _ => SanitizeLayerName(outputConfig.LayerOptions.CutLineLayer, "MK_剖切线")
        };
    }

    private static string SanitizeLayerName(string? configuredName, string fallback)
        => string.IsNullOrWhiteSpace(configuredName) ? fallback : configuredName.Trim();

    private static CadHatchDrawResult Failure(
        SectionHatchCategory category,
        string? patternName,
        string? effectivePatternName,
        string? layerName,
        string message,
        Exception? exception,
        int boundaryPointCount = 0)
        => new()
        {
            Succeeded = false,
            Category = category,
            PatternName = patternName,
            EffectivePatternName = effectivePatternName,
            LayerName = layerName,
            BoundaryPointCount = boundaryPointCount,
            Message = message,
            Exception = exception
        };

    private static void TryErase(DBObject? dbObject)
    {
        if (dbObject == null || dbObject.IsErased)
        {
            return;
        }

        try
        {
            dbObject.Erase();
        }
        catch
        {
            // Best-effort cleanup only; callers need the original hatch failure.
        }
    }

    private static string FormatException(Exception? exception)
    {
        return exception switch
        {
            null => string.Empty,
            AcadException acadException => $"ErrorStatus={acadException.ErrorStatus}; Message={acadException.Message}",
            _ => $"Message={exception.Message}"
        };
    }
}
