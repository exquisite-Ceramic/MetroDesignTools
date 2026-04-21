using MetroToolKits.Foundation.Core.Diagnostics;

namespace MetroToolKits.SectionGenerator.App.Diagnostics;

/// <summary>
/// GenSection 链路的错误码与诊断工厂。
/// </summary>
public static class SectionGenerationErrorCodes
{
    public const string InvalidSectionLine = "SectionGenerator.InputValidation.InvalidSectionLine";
    public const string FloorConfigMissing = "SectionGenerator.FloorConfigLoad.ConfigMissing";
    public const string NoRecognizedElements = "SectionGenerator.ElementRecognition.NoRecognizedElements";
    public const string UnsupportedEntityType = "SectionGenerator.ElementRecognition.UnsupportedEntityType";
    public const string ConversionFailed = "SectionGenerator.ElementRecognition.ConversionFailed";
    public const string NoIntersectingElements = "SectionGenerator.ElementRecognition.NoIntersectingElements";
    public const string EmptyGeometry = "SectionGenerator.SectionComposition.EmptyGeometry";
    public const string DrawFailed = "SectionGenerator.DrawingOutput.DrawFailed";
    public const string SnapshotSaveFailed = "SectionGenerator.SnapshotPersist.SaveFailed";
    public const string Unexpected = "SectionGenerator.System.Unexpected";
}

public static class SectionGenerationFailures
{
    public static OperationFailure InvalidSectionLine(string? technicalMessage = null) => new()
    {
        Code = SectionGenerationErrorCodes.InvalidSectionLine,
        Category = FailureCategory.UserInput,
        Stage = PipelineStage.InputValidation,
        Module = "GenSectionCommand",
        UserMessage = "请选择直线实体作为剖切线",
        TechnicalMessage = technicalMessage ?? "所选对象不是 AutoCAD Line。"
    };

    public static OperationFailure NoRecognizedElements(string technicalMessage) => new()
    {
        Code = SectionGenerationErrorCodes.NoRecognizedElements,
        Category = FailureCategory.DomainBusiness,
        Stage = PipelineStage.ElementRecognition,
        Module = "GenerateSectionUseCase",
        UserMessage = "未识别到任何可生成剖面的构件",
        TechnicalMessage = technicalMessage
    };

    public static OperationFailure EmptyGeometry(string technicalMessage) => new()
    {
        Code = SectionGenerationErrorCodes.EmptyGeometry,
        Category = FailureCategory.ApplicationFlow,
        Stage = PipelineStage.SectionComposition,
        Module = "GenerateSectionUseCase",
        UserMessage = "识别到了构件，但未生成可绘制的剖面主体",
        TechnicalMessage = technicalMessage
    };

    public static OperationFailure DrawFailed(string technicalMessage, Exception? innerException = null) => new()
    {
        Code = SectionGenerationErrorCodes.DrawFailed,
        Category = FailureCategory.Infrastructure,
        Stage = PipelineStage.DrawingOutput,
        Module = "CadDrawingService",
        UserMessage = "剖面绘制失败，请检查当前图纸环境",
        TechnicalMessage = technicalMessage,
        InnerException = innerException
    };

    public static OperationFailure SnapshotSaveFailed(string technicalMessage, Exception? innerException = null) => new()
    {
        Code = SectionGenerationErrorCodes.SnapshotSaveFailed,
        Category = FailureCategory.Infrastructure,
        Stage = PipelineStage.SnapshotPersist,
        Module = "XDataSnapshotRepository",
        UserMessage = "剖面已生成，但写入快照失败",
        TechnicalMessage = technicalMessage,
        InnerException = innerException
    };

    public static OperationFailure Unexpected(string module, PipelineStage stage, Exception innerException) => new()
    {
        Code = SectionGenerationErrorCodes.Unexpected,
        Category = FailureCategory.SystemInternal,
        Stage = stage,
        Module = module,
        UserMessage = "剖面生成过程中发生未预期错误",
        TechnicalMessage = innerException.Message,
        InnerException = innerException
    };
}

public static class SectionGenerationDiagnosticFactory
{
    public static OperationDiagnostic FloorConfigMissing() => new()
    {
        Level = DiagnosticLevel.Warning,
        Code = SectionGenerationErrorCodes.FloorConfigMissing,
        Stage = PipelineStage.FloorConfigLoad,
        Module = "GenerateSectionUseCase",
        Message = "未找到楼层配置文件，已回退到默认单层配置",
        Suggestion = "如需多楼层剖面，请先执行 FloorConfig 配置楼层。"
    };

    public static OperationDiagnostic NoRecognizedElements(string module, string message, string? floorName = null) => new()
    {
        Level = DiagnosticLevel.Warning,
        Code = SectionGenerationErrorCodes.NoRecognizedElements,
        Stage = PipelineStage.ElementRecognition,
        Module = module,
        Message = message,
        Suggestion = "检查剖切线是否穿过构件，以及图层/实体类型是否受支持。",
        Metadata = CreateMetadata(("floor", floorName))
    };

    public static OperationDiagnostic UnsupportedEntityType(
        string module,
        string? targetHandle,
        string layerName,
        string actualEntityType,
        string expectedEntityType) => new()
    {
        Level = DiagnosticLevel.Warning,
        Code = SectionGenerationErrorCodes.UnsupportedEntityType,
        Stage = PipelineStage.ElementRecognition,
        Module = module,
        Message = $"图层 {layerName} 命中了识别规则，但实体类型 {actualEntityType} 当前不受支持",
        TargetHandle = targetHandle,
        Suggestion = $"请改用 {expectedEntityType} 类型，或先通过图层/构件转换命令整理图元。",
        Metadata = CreateMetadata(
            ("layer", layerName),
            ("actualEntityType", actualEntityType),
            ("expectedEntityType", expectedEntityType))
    };

    public static OperationDiagnostic ConversionFailed(
        string module,
        string? targetHandle,
        string elementType,
        string reason) => new()
    {
        Level = DiagnosticLevel.Warning,
        Code = SectionGenerationErrorCodes.ConversionFailed,
        Stage = PipelineStage.ElementRecognition,
        Module = module,
        Message = $"构件 {elementType} 转换失败",
        TargetHandle = targetHandle,
        Suggestion = "请检查图元几何是否完整，或先清理异常对象。",
        Metadata = CreateMetadata(
            ("elementType", elementType),
            ("reason", reason))
    };

    public static OperationDiagnostic NoIntersectingElements(
        string module,
        string? targetHandle,
        string elementType,
        string layerName) => new()
    {
        Level = DiagnosticLevel.Warning,
        Code = SectionGenerationErrorCodes.NoIntersectingElements,
        Stage = PipelineStage.ElementRecognition,
        Module = module,
        Message = $"构件 {elementType} 位于支持图层 {layerName}，但未与剖切线相交",
        TargetHandle = targetHandle,
        Suggestion = "请调整剖切线位置，确保它真正穿过目标构件。",
        Metadata = CreateMetadata(
            ("elementType", elementType),
            ("layer", layerName))
    };

    private static IReadOnlyDictionary<string, string?> CreateMetadata(params (string Key, string? Value)[] entries)
    {
        var metadata = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in entries)
        {
            if (string.IsNullOrWhiteSpace(key) || value == null)
                continue;

            metadata[key] = value;
        }

        return metadata;
    }
}
