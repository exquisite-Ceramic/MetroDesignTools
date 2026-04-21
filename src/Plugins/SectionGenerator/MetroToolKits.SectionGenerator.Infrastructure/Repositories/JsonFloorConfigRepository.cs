using Microsoft.Extensions.Logging;
using MetroToolKits.SectionGenerator.App.Abstractions;
using MetroToolKits.SectionGenerator.Core.Sections;
using System.Text.Json;

namespace MetroToolKits.SectionGenerator.Infrastructure.Repositories;

/// <summary>
/// JSON 楼层配置仓储
/// </summary>
public sealed class JsonFloorConfigRepository : IFloorConfigRepository
{
    private static readonly JsonSerializerOptions ReadOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _filePath;
    private readonly string? _templatePath;
    private readonly ILogger<JsonFloorConfigRepository> _logger;

    public JsonFloorConfigRepository(
        string filePath,
        string? templatePath,
        ILogger<JsonFloorConfigRepository> logger)
    {
        _filePath = filePath;
        _templatePath = templatePath;
        _logger   = logger;
    }

    public SectionConfig Load()
    {
        _logger.LogDebug("加载配置文件: {FilePath}", _filePath);

        if (!File.Exists(_filePath))
        {
            if (TrySeedFromTemplate())
            {
                _logger.LogInformation("配置文件不存在，已从模板复制到用户目录: {FilePath}", _filePath);
            }
            else
            {
                _logger.LogWarning("配置文件不存在，使用默认配置: {FilePath}", _filePath);
                var defaultConfig = CreateDefault();
                Save(defaultConfig);
                return defaultConfig;
            }
        }

        try
        {
            var json = File.ReadAllText(_filePath);
            var config = JsonSerializer.Deserialize<SectionConfig>(json, ReadOptions) ?? CreateDefault();
            _logger.LogDebug("配置加载成功，楼层数: {FloorCount}", config.Floors.Count);
            return config;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "配置文件解析失败: {FilePath}", _filePath);
            return CreateDefault();
        }
    }

    public void Save(SectionConfig config)
    {
        var dir = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        File.WriteAllText(_filePath, JsonSerializer.Serialize(config, WriteOptions));
        _logger.LogDebug("配置已保存: {FilePath}", _filePath);
    }

    private bool TrySeedFromTemplate()
    {
        if (string.IsNullOrWhiteSpace(_templatePath) || !File.Exists(_templatePath))
        {
            return false;
        }

        var dir = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        File.Copy(_templatePath, _filePath, overwrite: false);
        return true;
    }

    private static SectionConfig CreateDefault() => new()
    {
        GlobalSlopeEnabled = true,
        GlobalSlopeValue   = 0.002,
        GlobalSlopeTarget  = "StructuralSlab",
        Floors = new List<FloorConfig>
        {
            new() { Name = "F1", Height = 5200, FinishThickness = 120,
                    BottomSlabThickness = 800, TopSlabThickness = 600,
                    HasSlope = true, SlopeValue = 0.002 }
        }
    };
}
