using MetroToolKits.SectionGenerator.App.Models;
using MetroToolKits.Foundation.Building.Types;
using MetroToolKits.SectionGenerator.Core.Sections;

namespace MetroToolKits.SectionGenerator.App.UseCases;

internal static class SectionExecutionConfigBuilder
{
    public static bool TryBuild(
        LoadedSectionConfig sourceDocument,
        IReadOnlyList<string> includedFloorNames,
        string? targetFloorName,
        out LoadedSectionConfig executionDocument,
        out string errorMessage)
    {
        ArgumentNullException.ThrowIfNull(sourceDocument);
        var sourceConfig = sourceDocument.Config;

        var floors = sourceConfig.Floors ?? new List<FloorConfig>();
        var requestedFloorNames = includedFloorNames.Count > 0
            ? includedFloorNames
            : string.IsNullOrWhiteSpace(targetFloorName)
                ? Array.Empty<string>()
                : new[] { targetFloorName };

        List<FloorConfig> selectedFloors;
        if (requestedFloorNames.Count == 0)
        {
            selectedFloors = floors.ToList();
        }
        else
        {
            var requestedSet = new HashSet<string>(requestedFloorNames, StringComparer.OrdinalIgnoreCase);
            selectedFloors = floors
                .Where(floor => requestedSet.Contains(floor.Name))
                .ToList();

            var missingFloors = requestedFloorNames
                .Where(name => selectedFloors.All(floor => !string.Equals(floor.Name, name, StringComparison.OrdinalIgnoreCase)))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (missingFloors.Count > 0)
            {
                executionDocument = new LoadedSectionConfig();
                errorMessage = $"未找到目标楼层: {string.Join(", ", missingFloors)}";
                return false;
            }
        }

        var executionFloors = selectedFloors
            .Select(floor => CloneFloor(floor, sourceConfig))
            .ToList();

        var executionBaseFloorName = executionFloors.Count switch
        {
            0 => string.Empty,
            1 => executionFloors[0].Name,
            _ => sourceConfig.AlignmentBaseFloorName
        };

        if (executionFloors.Count > 1 &&
            executionFloors.All(floor => !string.Equals(floor.Name, executionBaseFloorName, StringComparison.OrdinalIgnoreCase)))
        {
            executionDocument = new LoadedSectionConfig();
            errorMessage = "执行楼层集合不包含当前基准层，无法解析多楼层剖切线。";
            return false;
        }

        executionDocument = new LoadedSectionConfig
        {
            Config = new SectionConfig
            {
                GlobalSlopeEnabled = sourceConfig.GlobalSlopeEnabled,
                GlobalSlopeValue = sourceConfig.GlobalSlopeValue,
                GlobalSlopeTarget = sourceConfig.GlobalSlopeTarget,
                GlobalTopSlopeEnabled = sourceConfig.GlobalTopSlopeEnabled,
                GlobalTopSlopeValue = sourceConfig.GlobalTopSlopeValue,
                GlobalTopSlopeTarget = sourceConfig.GlobalTopSlopeTarget,
                GlobalBottomSlopeEnabled = sourceConfig.GlobalBottomSlopeEnabled,
                GlobalBottomSlopeValue = sourceConfig.GlobalBottomSlopeValue,
                GlobalBottomSlopeTarget = sourceConfig.GlobalBottomSlopeTarget,
                AlignmentBaseFloorName = executionBaseFloorName,
                Floors = executionFloors
            },
            OutputConfig = CloneOutputConfig(sourceDocument.OutputConfig),
            RuntimeDiagnostics = sourceDocument.RuntimeDiagnostics.ToList(),
            RuntimeState = CloneRuntimeState(sourceDocument.RuntimeState)
        };

        errorMessage = string.Empty;
        return true;
    }

    private static FloorConfig CloneFloor(FloorConfig floor, SectionConfig sourceConfig)
    {
        var clone = new FloorConfig
        {
            Name = floor.Name,
            Height = floor.Height,
            FinishThickness = floor.FinishThickness,
            BottomSlabThickness = floor.BottomSlabThickness,
            TopSlabThickness = floor.TopSlabThickness,
            HasSlope = floor.HasSlope,
            SlopeValue = floor.SlopeValue,
            SlopeTarget = floor.SlopeTarget,
            BottomBoundarySlab = CloneBoundarySlab(floor.BottomBoundarySlab),
            TopBoundarySlab = CloneBoundarySlab(floor.TopBoundarySlab),
            AlignmentPoints = floor.AlignmentPoints
                .Select(point => new Foundation.Core.Geometry.Point3D(point.X, point.Y, point.Z))
                .ToList(),
            ScopeBounds = floor.ScopeBounds
        };

        ApplyGlobalSlopeDefaults(clone, sourceConfig);
        return clone;
    }

    private static void ApplyGlobalSlopeDefaults(FloorConfig floor, SectionConfig sourceConfig)
    {
        if (!floor.TopBoundarySlab.SlopeEnabled && !floor.HasSlope)
        {
            var useGlobalTopSlope = sourceConfig.GlobalTopSlopeEnabled || sourceConfig.GlobalSlopeEnabled;
            if (useGlobalTopSlope)
            {
                floor.TopBoundarySlab.SlopeEnabled = true;
                floor.TopBoundarySlab.SlopeValue = sourceConfig.GlobalTopSlopeEnabled
                    ? sourceConfig.GlobalTopSlopeValue
                    : sourceConfig.GlobalSlopeValue;
                floor.TopBoundarySlab.SlopeTarget = sourceConfig.GlobalTopSlopeEnabled
                    ? sourceConfig.GlobalTopSlopeTarget
                    : sourceConfig.GlobalSlopeTarget;
            }
        }

        if (!floor.BottomBoundarySlab.SlopeEnabled && sourceConfig.GlobalBottomSlopeEnabled)
        {
            floor.BottomBoundarySlab.SlopeEnabled = true;
            floor.BottomBoundarySlab.SlopeValue = sourceConfig.GlobalBottomSlopeValue;
            floor.BottomBoundarySlab.SlopeTarget = sourceConfig.GlobalBottomSlopeTarget;
        }
    }

    private static BoundarySlabConfig CloneBoundarySlab(BoundarySlabConfig? boundarySlab)
        => new()
        {
            TemplateId = boundarySlab?.TemplateId ?? string.Empty,
            SlopeEnabled = boundarySlab?.SlopeEnabled == true,
            SlopeValue = boundarySlab?.SlopeValue ?? 0,
            SlopeTarget = boundarySlab?.SlopeTarget ?? "StructuralSlab"
        };

    private static SectionOutputConfig CloneOutputConfig(SectionOutputConfig? outputConfig)
    {
        outputConfig ??= new SectionOutputConfig();
        return new SectionOutputConfig
        {
            AnnotationOptions = new AnnotationOptions
            {
                GenerateAnnotations = outputConfig.AnnotationOptions?.GenerateAnnotations == true
            },
            HatchOptions = new HatchOptions
            {
                Enabled = outputConfig.HatchOptions?.Enabled == true,
                WallHatch = CloneHatchStyle(outputConfig.HatchOptions?.WallHatch),
                ColumnHatch = CloneHatchStyle(outputConfig.HatchOptions?.ColumnHatch),
                SlabHatch = CloneHatchStyle(outputConfig.HatchOptions?.SlabHatch)
            },
            LayerOptions = new LayerOptions
            {
                CutLineLayer = outputConfig.LayerOptions?.CutLineLayer ?? "MK_剖切线",
                SightLineLayer = outputConfig.LayerOptions?.SightLineLayer ?? "MK_看线",
                AnnotationLayer = outputConfig.LayerOptions?.AnnotationLayer ?? "MK_标注",
                WallHatchLayer = outputConfig.LayerOptions?.WallHatchLayer ?? "MK_墙填充",
                ColumnHatchLayer = outputConfig.LayerOptions?.ColumnHatchLayer ?? "MK_柱填充",
                SlabHatchLayer = outputConfig.LayerOptions?.SlabHatchLayer ?? "MK_楼板填充",
                StructuralLayer = outputConfig.LayerOptions?.StructuralLayer ?? "MK_结构输出",
                FinishLayer = outputConfig.LayerOptions?.FinishLayer ?? "MK_装修输出"
            }
        };
    }

    private static HatchStyleOptions CloneHatchStyle(HatchStyleOptions? style)
        => new()
        {
            PatternName = style?.PatternName ?? "ANSI31",
            Scale = style?.Scale ?? 100.0,
            Angle = style?.Angle ?? 0,
            UseByLayer = style?.UseByLayer != false
        };

    private static SectionConfigRuntimeState CloneRuntimeState(SectionConfigRuntimeState? state)
        => new()
        {
            Source = state?.Source ?? SectionConfigStorageSource.Missing,
            IsCurrentDrawingSaved = state?.IsCurrentDrawingSaved == true,
            HasPersistedConfig = state?.HasPersistedConfig == true,
            DrawingDisplayName = state?.DrawingDisplayName ?? string.Empty,
            DrawingPath = state?.DrawingPath
        };
}
