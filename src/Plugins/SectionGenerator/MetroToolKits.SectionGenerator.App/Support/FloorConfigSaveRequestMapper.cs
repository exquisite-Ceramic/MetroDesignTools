using MetroToolKits.Foundation.Building.Types;
using MetroToolKits.Foundation.Core.Geometry;
using MetroToolKits.SectionGenerator.App.Abstractions;
using MetroToolKits.SectionGenerator.App.Models;
using MetroToolKits.SectionGenerator.Contracts.Common;
using MetroToolKits.SectionGenerator.Contracts.Floors;
using MetroToolKits.SectionGenerator.Core.Sections;

namespace MetroToolKits.SectionGenerator.App.Support;

public sealed class FloorConfigSaveRequestMapper : IFloorConfigSaveRequestMapper
{
    public SaveFloorConfigRequestDto ToRequest(LoadedSectionConfig document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var config = document.Config ?? new SectionConfig();

        return new SaveFloorConfigRequestDto
        {
            AlignmentBaseFloorName = config.AlignmentBaseFloorName,
            GlobalSlopeEnabled = config.GlobalSlopeEnabled,
            GlobalSlopePercent = ToPercent(config.GlobalSlopeValue),
            GlobalSlopeTarget = config.GlobalSlopeTarget,
            GlobalTopSlopeEnabled = config.GlobalTopSlopeEnabled,
            GlobalTopSlopePercent = ToPercent(config.GlobalTopSlopeValue),
            GlobalTopSlopeTarget = config.GlobalTopSlopeTarget,
            GlobalBottomSlopeEnabled = config.GlobalBottomSlopeEnabled,
            GlobalBottomSlopePercent = ToPercent(config.GlobalBottomSlopeValue),
            GlobalBottomSlopeTarget = config.GlobalBottomSlopeTarget,
            Floors = (config.Floors ?? []).Select(MapFloorToDto).ToArray()
        };
    }

    public void ApplyToDocument(SaveFloorConfigRequestDto request, LoadedSectionConfig document)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(document);

        document.Config ??= new SectionConfig();

        document.Config.AlignmentBaseFloorName = request.AlignmentBaseFloorName ?? string.Empty;
        document.Config.GlobalSlopeEnabled = request.GlobalSlopeEnabled;
        document.Config.GlobalSlopeValue = ToDecimal(request.GlobalSlopePercent);
        document.Config.GlobalSlopeTarget = request.GlobalSlopeTarget ?? string.Empty;
        document.Config.GlobalTopSlopeEnabled = request.GlobalTopSlopeEnabled;
        document.Config.GlobalTopSlopeValue = ToDecimal(request.GlobalTopSlopePercent);
        document.Config.GlobalTopSlopeTarget = request.GlobalTopSlopeTarget ?? string.Empty;
        document.Config.GlobalBottomSlopeEnabled = request.GlobalBottomSlopeEnabled;
        document.Config.GlobalBottomSlopeValue = ToDecimal(request.GlobalBottomSlopePercent);
        document.Config.GlobalBottomSlopeTarget = request.GlobalBottomSlopeTarget ?? string.Empty;
        document.Config.Floors = request.Floors.Select(MapFloorToDomain).ToList();
    }

    private static FloorConfigEditDto MapFloorToDto(FloorConfig floor)
        => new()
        {
            Name = floor.Name,
            Height = floor.Height,
            FinishThickness = floor.FinishThickness,
            BottomSlabThickness = floor.BottomSlabThickness,
            TopSlabThickness = floor.TopSlabThickness,
            LegacySlopeEnabled = floor.HasSlope,
            LegacySlopePercent = ToPercent(floor.SlopeValue),
            LegacySlopeTarget = floor.SlopeTarget,
            AlignmentPoints = floor.AlignmentPoints.Select(MapPointToDto).ToArray(),
            ScopeBounds = MapScopeToDto(floor.ScopeBounds),
            TopBoundarySlab = MapBoundarySlabToDto(floor.TopBoundarySlab),
            BottomBoundarySlab = MapBoundarySlabToDto(floor.BottomBoundarySlab)
        };

    private static FloorConfig MapFloorToDomain(FloorConfigEditDto floor)
        => new()
        {
            Name = floor.Name ?? string.Empty,
            Height = floor.Height,
            FinishThickness = floor.FinishThickness,
            BottomSlabThickness = floor.BottomSlabThickness,
            TopSlabThickness = floor.TopSlabThickness,
            HasSlope = floor.LegacySlopeEnabled,
            SlopeValue = ToDecimal(floor.LegacySlopePercent),
            SlopeTarget = floor.LegacySlopeTarget ?? string.Empty,
            AlignmentPoints = floor.AlignmentPoints.Select(MapPointToDomain).ToList(),
            ScopeBounds = MapScopeToDomain(floor.ScopeBounds),
            TopBoundarySlab = MapBoundarySlabToDomain(floor.TopBoundarySlab),
            BottomBoundarySlab = MapBoundarySlabToDomain(floor.BottomBoundarySlab)
        };

    private static BoundarySlabEditDto MapBoundarySlabToDto(BoundarySlabConfig config)
        => new()
        {
            TemplateId = config.TemplateId,
            SlopeEnabled = config.SlopeEnabled,
            SlopePercent = ToPercent(config.SlopeValue),
            SlopeTarget = config.SlopeTarget
        };

    private static BoundarySlabConfig MapBoundarySlabToDomain(BoundarySlabEditDto config)
        => new()
        {
            TemplateId = config.TemplateId ?? string.Empty,
            SlopeEnabled = config.SlopeEnabled,
            SlopeValue = ToDecimal(config.SlopePercent),
            SlopeTarget = config.SlopeTarget ?? string.Empty
        };

    private static Point3DDto MapPointToDto(Point3D point)
        => new()
        {
            X = point.X,
            Y = point.Y,
            Z = point.Z
        };

    private static Point3D MapPointToDomain(Point3DDto point)
        => new(point.X, point.Y, point.Z);

    private static ScopeBoundsDto? MapScopeToDto(ScopeBounds2D? scopeBounds)
    {
        if (!scopeBounds.HasValue)
        {
            return null;
        }

        return new ScopeBoundsDto
        {
            MinX = scopeBounds.Value.MinX,
            MinY = scopeBounds.Value.MinY,
            MaxX = scopeBounds.Value.MaxX,
            MaxY = scopeBounds.Value.MaxY
        };
    }

    private static ScopeBounds2D? MapScopeToDomain(ScopeBoundsDto? scopeBounds)
    {
        if (scopeBounds == null)
        {
            return null;
        }

        return new ScopeBounds2D
        {
            MinX = scopeBounds.MinX,
            MinY = scopeBounds.MinY,
            MaxX = scopeBounds.MaxX,
            MaxY = scopeBounds.MaxY
        };
    }

    private static double ToPercent(double decimalValue) => decimalValue * 100.0;

    private static double ToDecimal(double percentValue) => percentValue / 100.0;
}
