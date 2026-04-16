using Microsoft.Extensions.Logging;
using MetroToolKits.SectionGenerator.App.Abstractions;
using MetroToolKits.SectionGenerator.Core.Sections;
using Newtonsoft.Json;

namespace MetroToolKits.SectionGenerator.Infrastructure.Repositories;

/// <summary>
/// JSON 楼层配置仓储
/// </summary>
public sealed class JsonFloorConfigRepository : IFloorConfigRepository
{
    private readonly string _filePath;
    private readonly ILogger<JsonFloorConfigRepository> _logger;

    public JsonFloorConfigRepository(string filePath, ILogger<JsonFloorConfigRepository> logger)
    {
        _filePath = filePath;
        _logger   = logger;
    }

    public SectionConfig Load()
    {
        _logger.LogDebug("加载配置文件: {FilePath}", _filePath);

        if (!File.Exists(_filePath))
        {
            _logger.LogWarning("配置文件不存在，使用默认配置: {FilePath}", _filePath);
            var defaultConfig = CreateDefault();
            Save(defaultConfig);
            return defaultConfig;
        }

        try
        {
            var json = File.ReadAllText(_filePath);
            var config = JsonConvert.DeserializeObject<SectionConfig>(json) ?? CreateDefault();
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
        File.WriteAllText(_filePath, JsonConvert.SerializeObject(config, Formatting.Indented));
        _logger.LogDebug("配置已保存: {FilePath}", _filePath);
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
