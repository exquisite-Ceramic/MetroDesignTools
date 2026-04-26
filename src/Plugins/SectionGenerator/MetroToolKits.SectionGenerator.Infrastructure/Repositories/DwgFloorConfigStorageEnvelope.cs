using System.Text.Json;
using MetroToolKits.Foundation.Core.Diagnostics;
using MetroToolKits.SectionGenerator.App.Models;
using MetroToolKits.SectionGenerator.Core.Sections;

namespace MetroToolKits.SectionGenerator.Infrastructure.Repositories;

internal static class DwgFloorConfigStorageEnvelope
{
    private static readonly JsonSerializerOptions ReadOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = true
    };

    internal const int CurrentSchemaVersion = 1;

    public static string Serialize(LoadedSectionConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        var storedDocument = new StoredSectionConfigDocument
        {
            SchemaVersion = CurrentSchemaVersion,
            ToolVersion = ResolveToolVersion(),
            SavedAt = DateTimeOffset.UtcNow,
            Config = config.Config ?? new SectionConfig(),
            OutputConfig = config.OutputConfig ?? new SectionOutputConfig()
        };

        return JsonSerializer.Serialize(storedDocument, WriteOptions);
    }

    public static bool TryDeserialize(
        string json,
        out LoadedSectionConfig? config,
        out IReadOnlyList<OperationDiagnostic> diagnostics)
    {
        config = null;
        diagnostics = Array.Empty<OperationDiagnostic>();

        if (string.IsNullOrWhiteSpace(json))
        {
            return false;
        }

        var storedDocument = JsonSerializer.Deserialize<StoredSectionConfigDocument>(json, ReadOptions);
        if (storedDocument == null)
        {
            return false;
        }

        var runtimeDiagnostics = new List<OperationDiagnostic>();
        if (storedDocument.SchemaVersion > CurrentSchemaVersion)
        {
            runtimeDiagnostics.Add(CreateFutureVersionDiagnostic(storedDocument.SchemaVersion, storedDocument.ToolVersion));
        }

        config = new LoadedSectionConfig
        {
            Config = storedDocument.Config ?? new SectionConfig(),
            OutputConfig = storedDocument.OutputConfig ?? new SectionOutputConfig(),
            RuntimeDiagnostics = runtimeDiagnostics
        };
        diagnostics = runtimeDiagnostics;
        return true;
    }

    private static string ResolveToolVersion()
        => typeof(DwgFloorConfigStorageEnvelope).Assembly.GetName().Version?.ToString() ?? string.Empty;

    private static OperationDiagnostic CreateFutureVersionDiagnostic(int schemaVersion, string? toolVersion)
        => new()
        {
            Level = DiagnosticLevel.Warning,
            Code = "SectionGenerator.DwgConfig.FutureSchemaVersion",
            Stage = PipelineStage.FloorConfigLoad,
            Module = nameof(DwgFloorConfigStorageEnvelope),
            Message = $"检测到较新的 DWG 配置版本 v{schemaVersion}，已尽力按当前版本兼容读取。",
            Suggestion = string.IsNullOrWhiteSpace(toolVersion)
                ? "若读取结果异常，请使用较新的插件版本重新打开图纸。"
                : $"该配置由工具版本 {toolVersion} 写入；若读取结果异常，请使用相同或更高版本插件重试。"
        };

    private sealed class StoredSectionConfigDocument
    {
        public int SchemaVersion { get; set; }

        public string ToolVersion { get; set; } = string.Empty;

        public DateTimeOffset SavedAt { get; set; }

        public SectionConfig Config { get; set; } = new();

        public SectionOutputConfig OutputConfig { get; set; } = new();
    }
}
