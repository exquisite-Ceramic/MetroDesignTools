using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using MetroToolKits.Foundation.Building.Types;
using MetroToolKits.SectionGenerator.Core.Sections;

namespace MetroToolKits.SectionGenerator.Infrastructure.Repositories;

/// <summary>
/// 基于 JSON 的楼板模板目录。
/// </summary>
public sealed class JsonSlabAssemblyTemplateCatalog : ISlabAssemblyTemplateCatalog
{
    private static readonly JsonSerializerOptions ReadOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly string _configPath;
    private readonly string? _templatePath;
    private readonly ILogger<JsonSlabAssemblyTemplateCatalog> _logger;
    private List<SlabAssemblyTemplate>? _cache;
    private Dictionary<string, SlabAssemblyTemplate>? _map;

    public JsonSlabAssemblyTemplateCatalog(
        string configPath,
        string? templatePath,
        ILogger<JsonSlabAssemblyTemplateCatalog> logger)
    {
        _configPath = configPath;
        _templatePath = templatePath;
        _logger = logger;
    }

    public IReadOnlyList<SlabAssemblyTemplate> GetAllTemplates()
    {
        EnsureLoaded();
        return _cache!;
    }

    public SlabAssemblyTemplate? GetById(string templateId)
    {
        EnsureLoaded();
        return string.IsNullOrWhiteSpace(templateId)
            ? null
            : _map!.TryGetValue(templateId, out var template)
                ? template
                : null;
    }

    public void SaveAll(IReadOnlyCollection<SlabAssemblyTemplate> templates)
    {
        var normalized = templates
            .Where(template => !string.IsNullOrWhiteSpace(template.TemplateId))
            .Select(CloneTemplate)
            .ToList();

        var directory = Path.GetDirectoryName(_configPath);
        if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(_configPath, JsonSerializer.Serialize(normalized, WriteOptions));
        _cache = normalized;
        _map = _cache.ToDictionary(template => template.TemplateId, template => template, StringComparer.OrdinalIgnoreCase);
    }

    private void EnsureLoaded()
    {
        if (_cache != null)
        {
            return;
        }

        var templates = LoadOrCreateTemplates();
        _cache = templates.Select(CloneTemplate).ToList();
        _map = _cache.ToDictionary(template => template.TemplateId, template => template, StringComparer.OrdinalIgnoreCase);
    }

    private IReadOnlyList<SlabAssemblyTemplate> LoadOrCreateTemplates()
    {
        if (!File.Exists(_configPath))
        {
            if (!TrySeedFromTemplate())
            {
                var defaults = CreateDefaultTemplates();
                SaveAll(defaults);
                return defaults;
            }
        }

        try
        {
            var json = File.ReadAllText(_configPath);
            return JsonSerializer.Deserialize<List<SlabAssemblyTemplate>>(json, ReadOptions)
                   ?? CreateDefaultTemplates();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "楼板模板配置解析失败，使用默认模板。");
            return CreateDefaultTemplates();
        }
    }

    private bool TrySeedFromTemplate()
    {
        if (string.IsNullOrWhiteSpace(_templatePath) || !File.Exists(_templatePath))
        {
            return false;
        }

        var directory = Path.GetDirectoryName(_configPath);
        if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.Copy(_templatePath, _configPath, overwrite: false);
        return true;
    }

    private static SlabAssemblyTemplate CloneTemplate(SlabAssemblyTemplate template)
    {
        return new SlabAssemblyTemplate
        {
            TemplateId = template.TemplateId,
            TemplateName = template.TemplateName,
            WallJunctionMode = template.WallJunctionMode,
            CoreRule = new SlabCoreRule
            {
                Name = template.CoreRule.Name,
                Thickness = template.CoreRule.Thickness,
                MaterialOrCategory = template.CoreRule.MaterialOrCategory,
                VisibleInSection = template.CoreRule.VisibleInSection
            },
            TopLayers = template.TopLayers.Select(CloneRule).ToList(),
            BottomLayers = template.BottomLayers.Select(CloneRule).ToList()
        };
    }

    private static SlabLayerRule CloneRule(SlabLayerRule rule)
    {
        return new SlabLayerRule
        {
            Name = rule.Name,
            Side = rule.Side,
            Order = rule.Order,
            Thickness = rule.Thickness,
            MaterialOrCategory = rule.MaterialOrCategory,
            VisibleInSection = rule.VisibleInSection
        };
    }

    private static List<SlabAssemblyTemplate> CreateDefaultTemplates()
    {
        return new List<SlabAssemblyTemplate>
        {
            new()
            {
                TemplateId = "slab-120-finish",
                TemplateName = "120结构板-上部面层",
                WallJunctionMode = SlabWallJunctionMode.StopAtWallFace,
                CoreRule = new SlabCoreRule
                {
                    Name = "结构板",
                    Thickness = 120,
                    MaterialOrCategory = "结构",
                    VisibleInSection = true
                },
                TopLayers = new List<SlabLayerRule>
                {
                    new()
                    {
                        Name = "找平层",
                        Side = SlabLayerSide.Top,
                        Order = 1,
                        Thickness = 20,
                        MaterialOrCategory = "找平",
                        VisibleInSection = true
                    },
                    new()
                    {
                        Name = "面层",
                        Side = SlabLayerSide.Top,
                        Order = 2,
                        Thickness = 10,
                        MaterialOrCategory = "装修",
                        VisibleInSection = true
                    }
                },
                BottomLayers = new List<SlabLayerRule>()
            }
        };
    }
}
