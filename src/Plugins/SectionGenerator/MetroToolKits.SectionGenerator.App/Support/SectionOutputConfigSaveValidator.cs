using MetroToolKits.Foundation.Core.Diagnostics;
using MetroToolKits.SectionGenerator.App.Models;

namespace MetroToolKits.SectionGenerator.App.Support;

internal static class SectionOutputConfigSaveValidator
{
    private const string ValidationCodePrefix = "SectionGenerator.OutputConfigSave";

    public static IReadOnlyList<OperationDiagnostic> Validate(SectionOutputConfig outputConfig)
    {
        ArgumentNullException.ThrowIfNull(outputConfig);

        var diagnostics = new List<OperationDiagnostic>();
        var layers = outputConfig.LayerOptions ?? new LayerOptions();
        var hatchOptions = outputConfig.HatchOptions ?? new HatchOptions();

        ValidateRequiredLayer(diagnostics, layers.CutLineLayer, "剖切线图层");
        ValidateRequiredLayer(diagnostics, layers.SightLineLayer, "可见线图层");
        ValidateRequiredLayer(diagnostics, layers.AnnotationLayer, "标注图层");
        ValidateRequiredLayer(diagnostics, layers.WallHatchLayer, "墙填充图层");
        ValidateRequiredLayer(diagnostics, layers.ColumnHatchLayer, "柱填充图层");
        ValidateRequiredLayer(diagnostics, layers.SlabHatchLayer, "楼板填充图层");
        ValidateRequiredLayer(diagnostics, layers.StructuralLayer, "结构输出图层");
        ValidateRequiredLayer(diagnostics, layers.FinishLayer, "装修输出图层");

        ValidateHatchStyle(diagnostics, hatchOptions.WallHatch, "墙填充");
        ValidateHatchStyle(diagnostics, hatchOptions.ColumnHatch, "柱填充");
        ValidateHatchStyle(diagnostics, hatchOptions.SlabHatch, "楼板填充");

        return diagnostics;
    }

    private static void ValidateRequiredLayer(
        ICollection<OperationDiagnostic> diagnostics,
        string? layerName,
        string displayName)
    {
        if (!string.IsNullOrWhiteSpace(layerName))
        {
            return;
        }

        diagnostics.Add(CreateValidationDiagnostic(
            $"{ValidationCodePrefix}.LayerMissing",
            $"{displayName}不能为空。"));
    }

    private static void ValidateHatchStyle(
        ICollection<OperationDiagnostic> diagnostics,
        HatchStyleOptions? hatchStyle,
        string displayName)
    {
        var resolvedStyle = hatchStyle ?? HatchStyleOptions.CreateDefault();
        if (string.IsNullOrWhiteSpace(resolvedStyle.PatternName))
        {
            diagnostics.Add(CreateValidationDiagnostic(
                $"{ValidationCodePrefix}.PatternMissing",
                $"{displayName}图案名称不能为空。"));
        }

        if (resolvedStyle.Scale <= 0)
        {
            diagnostics.Add(CreateValidationDiagnostic(
                $"{ValidationCodePrefix}.ScaleInvalid",
                $"{displayName}填充比例必须大于 0。"));
        }
    }

    private static OperationDiagnostic CreateValidationDiagnostic(string code, string message)
        => new()
        {
            Level = DiagnosticLevel.Error,
            Code = code,
            Stage = PipelineStage.InputValidation,
            Module = nameof(SectionOutputConfigSaveValidator),
            Message = message,
            Suggestion = "请修正输出配置中的无效字段后重试保存。"
        };
}
