using MetroToolKits.Foundation.Core.Diagnostics;
using MetroToolKits.SectionGenerator.App.Diagnostics;
using MetroToolKits.SectionGenerator.Core.Sections;

namespace MetroToolKits.SectionGenerator.App.Support;

internal static class SectionGenerationConfigValidator
{
    public static IReadOnlyList<OperationDiagnostic> ValidateForGeneration(
        SectionConfig? config,
        bool requireBaseScope = true)
    {
        var diagnostics = new List<OperationDiagnostic>();
        if (config == null)
        {
            diagnostics.Add(CreateValidationDiagnostic(
                "楼层配置为空，无法执行剖面生成。",
                SectionGenerationErrorCodes.FloorConfigMissing));
            return diagnostics;
        }

        var floors = config.Floors ?? new List<FloorConfig>();
        if (floors.Count <= 1)
        {
            return diagnostics;
        }

        if (string.IsNullOrWhiteSpace(config.AlignmentBaseFloorName))
        {
            diagnostics.Add(CreateValidationDiagnostic(
                "多楼层模式下必须选择基准层。",
                SectionGenerationErrorCodes.AlignmentBaseFloorMissing));
            return diagnostics;
        }

        var baseFloor = floors.FirstOrDefault(floor =>
            string.Equals(floor.Name, config.AlignmentBaseFloorName, StringComparison.OrdinalIgnoreCase));
        if (baseFloor == null)
        {
            diagnostics.Add(CreateValidationDiagnostic(
                $"未找到名为 {config.AlignmentBaseFloorName} 的基准层。",
                SectionGenerationErrorCodes.AlignmentBaseFloorMissing));
            return diagnostics;
        }

        if (!FloorSectionLineTransformer.TryValidateAlignmentPoints(baseFloor.AlignmentPoints, out var baseError))
        {
            diagnostics.Add(CreateValidationDiagnostic(
                $"基准层 {baseFloor.Name} 的对齐点无效: {baseError}",
                SectionGenerationErrorCodes.AlignmentBaseFloorInvalid));
            return diagnostics;
        }

        if (!baseFloor.ScopeBounds.HasValue)
        {
            if (requireBaseScope)
            {
                diagnostics.Add(CreateValidationDiagnostic(
                    $"基准层 {baseFloor.Name} 缺少整层范围框。",
                    SectionGenerationErrorCodes.FloorScopeMissing));
            }

            return diagnostics;
        }

        if (!baseFloor.ScopeBounds.Value.IsValid())
        {
            diagnostics.Add(CreateValidationDiagnostic(
                $"基准层 {baseFloor.Name} 的整层范围框无效。",
                SectionGenerationErrorCodes.FloorScopeInvalid));
        }

        return diagnostics;
    }

    private static OperationDiagnostic CreateValidationDiagnostic(string message, string code)
        => new()
        {
            Level = DiagnosticLevel.Error,
            Code = code,
            Stage = PipelineStage.FloorConfigLoad,
            Module = nameof(SectionGenerationConfigValidator),
            Message = message,
            Suggestion = "请先在楼层配置中补齐基准层、基准点和整层范围。"
        };
}
