using MetroToolKits.Foundation.Core.Diagnostics;
using MetroToolKits.SectionGenerator.Core.Sections;

namespace MetroToolKits.SectionGenerator.App.Support;

internal static class SectionGenerationConfigValidator
{
    public static IReadOnlyList<OperationDiagnostic> ValidateForGeneration(SectionConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        var diagnostics = new List<OperationDiagnostic>();
        if (config.Floors.Count <= 1)
        {
            return diagnostics;
        }

        var baseFloor = config.Floors.FirstOrDefault(floor =>
            string.Equals(floor.Name, config.AlignmentBaseFloorName, StringComparison.OrdinalIgnoreCase));
        if (baseFloor == null)
        {
            diagnostics.Add(CreateValidationDiagnostic("多楼层模式下必须选择基准层。"));
            return diagnostics;
        }

        if (!FloorSectionLineTransformer.TryValidateAlignmentPoints(baseFloor.AlignmentPoints, out var baseError))
        {
            diagnostics.Add(CreateValidationDiagnostic($"基准层 {baseFloor.Name} 的对齐点无效: {baseError}"));
            return diagnostics;
        }

        if (!baseFloor.ScopeBounds.HasValue)
        {
            diagnostics.Add(CreateValidationDiagnostic($"基准层 {baseFloor.Name} 缺少整层范围框。"));
            return diagnostics;
        }

        if (!baseFloor.ScopeBounds.Value.IsValid())
        {
            diagnostics.Add(CreateValidationDiagnostic($"基准层 {baseFloor.Name} 的整层范围框无效。"));
        }

        return diagnostics;
    }

    private static OperationDiagnostic CreateValidationDiagnostic(string message)
        => new()
        {
            Level = DiagnosticLevel.Error,
            Code = "SectionGenerator.FloorConfig.ValidationFailed",
            Stage = PipelineStage.FloorConfigLoad,
            Module = nameof(SectionGenerationConfigValidator),
            Message = message,
            Suggestion = "请先在楼层配置中补齐基准层、基准点和整层范围。"
        };
}
