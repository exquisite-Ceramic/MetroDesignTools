using MetroToolKits.Foundation.Core.Diagnostics;

namespace MetroToolKits.SectionGenerator.App.Diagnostics;

/// <summary>
/// GenSection 链路的错误码与诊断工厂。
/// </summary>
public static class SectionGenerationErrorCodes
{
    public const string InvalidSectionLine = "SectionGenerator.InputValidation.InvalidSectionLine";
    public const string FloorConfigMissing = "SectionGenerator.FloorConfigLoad.ConfigMissing";
    public const string AlignmentBaseFloorMissing = "SectionGenerator.FloorConfigLoad.AlignmentBaseFloorMissing";
    public const string AlignmentBaseFloorInvalid = "SectionGenerator.FloorConfigLoad.AlignmentBaseFloorInvalid";
    public const string AlignmentPointsMissing = "SectionGenerator.FloorConfigLoad.AlignmentPointsMissing";
    public const string AlignmentPointsInvalid = "SectionGenerator.FloorConfigLoad.AlignmentPointsInvalid";
    public const string AlignmentMigrationConflict = "SectionGenerator.FloorConfigLoad.AlignmentMigrationConflict";
    public const string FloorScopeMissing = "SectionGenerator.FloorConfigLoad.FloorScopeMissing";
    public const string FloorScopeInvalid = "SectionGenerator.FloorConfigLoad.FloorScopeInvalid";
    public const string TargetFloorInvalid = "SectionGenerator.InputValidation.TargetFloorInvalid";
    public const string LocalScopeInvalid = "SectionGenerator.InputValidation.LocalScopeInvalid";
    public const string NoRecognizedElements = "SectionGenerator.ElementRecognition.NoRecognizedElements";
    public const string UnsupportedEntityType = "SectionGenerator.ElementRecognition.UnsupportedEntityType";
    public const string ConversionFailed = "SectionGenerator.ElementRecognition.ConversionFailed";
    public const string NoIntersectingElements = "SectionGenerator.ElementRecognition.NoIntersectingElements";
    public const string AmbiguousWallPairing = "SectionGenerator.ElementRecognition.AmbiguousWallPairing";
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

    public static OperationFailure AlignmentBaseFloorMissing(string technicalMessage) => new()
    {
        Code = SectionGenerationErrorCodes.AlignmentBaseFloorMissing,
        Category = FailureCategory.UserInput,
        Stage = PipelineStage.FloorConfigLoad,
        Module = "FloorAlignmentResolver",
        UserMessage = "多楼层模式下必须先配置基准层",
        TechnicalMessage = technicalMessage
    };

    public static OperationFailure AlignmentBaseFloorInvalid(string technicalMessage) => new()
    {
        Code = SectionGenerationErrorCodes.AlignmentBaseFloorInvalid,
        Category = FailureCategory.UserInput,
        Stage = PipelineStage.FloorConfigLoad,
        Module = "FloorAlignmentResolver",
        UserMessage = "基准层配置无效，请检查基准层名称和对齐点",
        TechnicalMessage = technicalMessage
    };

    public static OperationFailure TargetFloorInvalid(string technicalMessage) => new()
    {
        Code = SectionGenerationErrorCodes.TargetFloorInvalid,
        Category = FailureCategory.UserInput,
        Stage = PipelineStage.InputValidation,
        Module = "GenerateSectionCommand",
        UserMessage = "目标楼层无效，请重新选择楼层",
        TechnicalMessage = technicalMessage
    };

    public static OperationFailure FloorScopeInvalid(string technicalMessage, bool isBaseFloor) => new()
    {
        Code = isBaseFloor
            ? SectionGenerationErrorCodes.FloorScopeInvalid
            : SectionGenerationErrorCodes.FloorScopeMissing,
        Category = FailureCategory.UserInput,
        Stage = PipelineStage.FloorConfigLoad,
        Module = "FloorScopeResolver",
        UserMessage = isBaseFloor
            ? "基准层整层范围无效，请先在 FloorConfig 中重新配置"
            : "楼层范围配置无效，请先检查 FloorConfig",
        TechnicalMessage = technicalMessage
    };

    public static OperationFailure LocalScopeInvalid(string technicalMessage) => new()
    {
        Code = SectionGenerationErrorCodes.LocalScopeInvalid,
        Category = FailureCategory.UserInput,
        Stage = PipelineStage.InputValidation,
        Module = "GenSectionCommand",
        UserMessage = "局部范围无效，请重新选择局部图元",
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
        Message = "当前图纸未找到楼层配置，已回退到默认单层配置",
        Suggestion = "如需多楼层剖面，请先执行 FloorConfig，为当前图纸配置基准点和整层范围。"
    };

    public static OperationDiagnostic AlignmentPointsMissing(string floorName, bool isBaseFloor = false) => new()
    {
        Level = DiagnosticLevel.Warning,
        Code = isBaseFloor
            ? SectionGenerationErrorCodes.AlignmentBaseFloorInvalid
            : SectionGenerationErrorCodes.AlignmentPointsMissing,
        Stage = PipelineStage.FloorConfigLoad,
        Module = "FloorAlignmentResolver",
        Message = isBaseFloor
            ? $"基准层 {floorName} 缺少对齐点"
            : $"楼层 {floorName} 缺少对齐点，已跳过该楼层",
        Suggestion = isBaseFloor
            ? "请在 FloorConfig 中为基准层拾取 3 个有效对齐点。"
            : "请在 FloorConfig 中为该楼层补齐与基准层对应的 3 个对齐点。",
        Metadata = CreateMetadata(("floor", floorName))
    };

    public static OperationDiagnostic AlignmentPointsInvalid(string floorName, string reason, bool isBaseFloor = false) => new()
    {
        Level = DiagnosticLevel.Warning,
        Code = isBaseFloor
            ? SectionGenerationErrorCodes.AlignmentBaseFloorInvalid
            : SectionGenerationErrorCodes.AlignmentPointsInvalid,
        Stage = PipelineStage.FloorConfigLoad,
        Module = "FloorAlignmentResolver",
        Message = isBaseFloor
            ? $"基准层 {floorName} 的对齐点无效: {reason}"
            : $"楼层 {floorName} 的对齐点无效，已跳过该楼层: {reason}",
        Suggestion = "请重新拾取原点、X 方向点、Y 方向点，确保三点不重复且不共线。",
        Metadata = CreateMetadata(
            ("floor", floorName),
            ("reason", reason))
    };

    public static OperationDiagnostic AlignmentMigrationConflict(string floorName, string reason) => new()
    {
        Level = DiagnosticLevel.Warning,
        Code = SectionGenerationErrorCodes.AlignmentMigrationConflict,
        Stage = PipelineStage.FloorConfigLoad,
        Module = "JsonFloorConfigRepository",
        Message = $"楼层 {floorName} 的旧版对齐数据与全局基准不一致，已清空本层对齐点待人工确认",
        Suggestion = "请打开 FloorConfig，重新确认该楼层的 3 个对齐点后再保存。",
        Metadata = CreateMetadata(
            ("floor", floorName),
            ("reason", reason))
    };

    public static OperationDiagnostic FloorScopeMissing(string floorName, bool isBaseFloor = false) => new()
    {
        Level = DiagnosticLevel.Warning,
        Code = SectionGenerationErrorCodes.FloorScopeMissing,
        Stage = PipelineStage.FloorConfigLoad,
        Module = "FloorScopeResolver",
        Message = isBaseFloor
            ? $"基准层 {floorName} 缺少整层范围框"
            : $"楼层 {floorName} 缺少整层范围框，已跳过该楼层",
        Suggestion = "请在 FloorConfig 中选择该楼层全部图元，自动生成整层范围框。",
        Metadata = CreateMetadata(("floor", floorName))
    };

    public static OperationDiagnostic FloorScopeInvalid(string floorName, string reason, bool isBaseFloor = false) => new()
    {
        Level = DiagnosticLevel.Warning,
        Code = SectionGenerationErrorCodes.FloorScopeInvalid,
        Stage = PipelineStage.FloorConfigLoad,
        Module = "FloorScopeResolver",
        Message = isBaseFloor
            ? $"基准层 {floorName} 的整层范围框无效: {reason}"
            : $"楼层 {floorName} 的整层范围框无效，已跳过该楼层: {reason}",
        Suggestion = "请重新选择该楼层的全部图元，重新生成整层范围框。",
        Metadata = CreateMetadata(
            ("floor", floorName),
            ("reason", reason))
    };

    public static OperationDiagnostic LocalScopeInvalid(string floorName, string reason) => new()
    {
        Level = DiagnosticLevel.Warning,
        Code = SectionGenerationErrorCodes.LocalScopeInvalid,
        Stage = PipelineStage.InputValidation,
        Module = "FloorScopeResolver",
        Message = $"楼层 {floorName} 的局部范围不可用，已跳过该楼层: {reason}",
        Suggestion = "请检查局部范围是否定义在正确楼层，并确保它与整层范围存在交集。",
        Metadata = CreateMetadata(
            ("floor", floorName),
            ("reason", reason))
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

    public static OperationDiagnostic AmbiguousWallPairing(
        string module,
        string layerName,
        string templateId,
        int intersectionCount) => new()
    {
        Level = DiagnosticLevel.Warning,
        Code = SectionGenerationErrorCodes.AmbiguousWallPairing,
        Stage = PipelineStage.ElementRecognition,
        Module = module,
        Message = $"墙图层 {layerName} 在模板 {templateId} 下产生了 {intersectionCount} 个交点，无法稳定配对为墙体",
        Suggestion = "请检查墙边界是否成对、是否存在贴墙共享边或异常断裂线。",
        Metadata = CreateMetadata(
            ("layer", layerName),
            ("templateId", templateId),
            ("intersectionCount", intersectionCount.ToString()))
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
