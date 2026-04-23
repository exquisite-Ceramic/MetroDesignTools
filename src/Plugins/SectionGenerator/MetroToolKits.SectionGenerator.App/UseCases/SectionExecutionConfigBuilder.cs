using MetroToolKits.SectionGenerator.Core.Sections;

namespace MetroToolKits.SectionGenerator.App.UseCases;

internal static class SectionExecutionConfigBuilder
{
    public static bool TryBuild(
        SectionConfig sourceConfig,
        IReadOnlyList<string> includedFloorNames,
        string? targetFloorName,
        out SectionConfig executionConfig,
        out string errorMessage)
    {
        ArgumentNullException.ThrowIfNull(sourceConfig);

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
                executionConfig = new SectionConfig();
                errorMessage = $"未找到目标楼层: {string.Join(", ", missingFloors)}";
                return false;
            }
        }

        var executionBaseFloorName = selectedFloors.Count switch
        {
            0 => string.Empty,
            1 => selectedFloors[0].Name,
            _ => sourceConfig.AlignmentBaseFloorName
        };

        if (selectedFloors.Count > 1 &&
            selectedFloors.All(floor => !string.Equals(floor.Name, executionBaseFloorName, StringComparison.OrdinalIgnoreCase)))
        {
            executionConfig = new SectionConfig();
            errorMessage = "执行楼层集合不包含当前基准层，无法解析多楼层剖切线。";
            return false;
        }

        executionConfig = new SectionConfig
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
            Floors = selectedFloors,
            RuntimeDiagnostics = sourceConfig.RuntimeDiagnostics.ToList()
        };

        errorMessage = string.Empty;
        return true;
    }
}
