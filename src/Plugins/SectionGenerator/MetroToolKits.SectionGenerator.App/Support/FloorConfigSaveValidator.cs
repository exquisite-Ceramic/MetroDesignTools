using MetroToolKits.Foundation.Core.Diagnostics;
using MetroToolKits.SectionGenerator.Core.Sections;

namespace MetroToolKits.SectionGenerator.App.Support;

internal static class FloorConfigSaveValidator
{
    private const string ValidationCodePrefix = "SectionGenerator.FloorConfigSave";

    public static IReadOnlyList<OperationDiagnostic> Validate(
        SectionConfig config,
        ISlabAssemblyTemplateCatalog slabTemplateCatalog)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(slabTemplateCatalog);

        var diagnostics = new List<OperationDiagnostic>();
        var floorNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        ValidateSlopeRange(
            diagnostics,
            config.GlobalSlopeValue,
            $"{ValidationCodePrefix}.GlobalSlopeInvalid",
            "图纸级全局坡度必须在 -100% ~ 100% 之间。");
        ValidateSlopeRange(
            diagnostics,
            config.GlobalTopSlopeValue,
            $"{ValidationCodePrefix}.GlobalTopSlopeInvalid",
            "图纸级顶板全局坡度必须在 -100% ~ 100% 之间。");
        ValidateSlopeRange(
            diagnostics,
            config.GlobalBottomSlopeValue,
            $"{ValidationCodePrefix}.GlobalBottomSlopeInvalid",
            "图纸级底板全局坡度必须在 -100% ~ 100% 之间。");

        foreach (var floor in config.Floors)
        {
            var floorName = floor.Name?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(floorName))
            {
                diagnostics.Add(CreateValidationDiagnostic(
                    $"{ValidationCodePrefix}.FloorNameMissing",
                    "楼层名称不能为空。"));
            }
            else if (!floorNames.Add(floorName))
            {
                diagnostics.Add(CreateValidationDiagnostic(
                    $"{ValidationCodePrefix}.FloorNameDuplicate",
                    $"楼层名称重复: {floorName}。"));
            }

            if (floor.Height <= 0)
            {
                diagnostics.Add(CreateValidationDiagnostic(
                    $"{ValidationCodePrefix}.FloorHeightInvalid",
                    $"楼层 {DisplayFloorName(floorName)} 的层高必须大于 0。"));
            }

            ValidateSlopeRange(
                diagnostics,
                floor.SlopeValue,
                $"{ValidationCodePrefix}.LegacySlopeInvalid",
                $"楼层 {DisplayFloorName(floorName)} 的兼容坡度必须在 -100% ~ 100% 之间。");
            ValidateSlopeRange(
                diagnostics,
                floor.TopBoundarySlab.SlopeValue,
                $"{ValidationCodePrefix}.TopBoundarySlopeInvalid",
                $"楼层 {DisplayFloorName(floorName)} 的顶边界板坡度必须在 -100% ~ 100% 之间。");
            ValidateSlopeRange(
                diagnostics,
                floor.BottomBoundarySlab.SlopeValue,
                $"{ValidationCodePrefix}.BottomBoundarySlopeInvalid",
                $"楼层 {DisplayFloorName(floorName)} 的底边界板坡度必须在 -100% ~ 100% 之间。");

            if (floor.ScopeBounds.HasValue)
            {
                var scopeBounds = floor.ScopeBounds.Value;
                var hasOrderedBounds = scopeBounds.MinX < scopeBounds.MaxX && scopeBounds.MinY < scopeBounds.MaxY;
                if (!hasOrderedBounds || !scopeBounds.IsValid())
                {
                    diagnostics.Add(CreateValidationDiagnostic(
                        $"{ValidationCodePrefix}.ScopeInvalid",
                        $"楼层 {DisplayFloorName(floorName)} 的范围框无效。"));
                }
            }

            ValidateTemplateId(
                diagnostics,
                floor.TopBoundarySlab.TemplateId,
                slabTemplateCatalog,
                $"{ValidationCodePrefix}.TopBoundaryTemplateMissing",
                $"楼层 {DisplayFloorName(floorName)} 的顶边界板模板不存在。");
            ValidateTemplateId(
                diagnostics,
                floor.BottomBoundarySlab.TemplateId,
                slabTemplateCatalog,
                $"{ValidationCodePrefix}.BottomBoundaryTemplateMissing",
                $"楼层 {DisplayFloorName(floorName)} 的底边界板模板不存在。");
        }

        return diagnostics;
    }

    private static void ValidateSlopeRange(
        ICollection<OperationDiagnostic> diagnostics,
        double decimalSlopeValue,
        string code,
        string message)
    {
        if (decimalSlopeValue < -1.0 || decimalSlopeValue > 1.0)
        {
            diagnostics.Add(CreateValidationDiagnostic(code, message));
        }
    }

    private static void ValidateTemplateId(
        ICollection<OperationDiagnostic> diagnostics,
        string? templateId,
        ISlabAssemblyTemplateCatalog slabTemplateCatalog,
        string code,
        string message)
    {
        var normalizedTemplateId = templateId?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedTemplateId))
        {
            return;
        }

        if (slabTemplateCatalog.GetById(normalizedTemplateId) == null)
        {
            diagnostics.Add(CreateValidationDiagnostic(code, message));
        }
    }

    private static string DisplayFloorName(string? floorName)
        => string.IsNullOrWhiteSpace(floorName) ? "（未命名楼层）" : floorName;

    private static OperationDiagnostic CreateValidationDiagnostic(string code, string message)
        => new()
        {
            Level = DiagnosticLevel.Error,
            Code = code,
            Stage = PipelineStage.InputValidation,
            Module = nameof(FloorConfigSaveValidator),
            Message = message,
            Suggestion = "请修正楼层配置中的无效字段后重试保存。"
        };
}
