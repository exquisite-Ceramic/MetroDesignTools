using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using MetroToolKits.Foundation.Building.Types;
using MetroToolKits.SectionGenerator.App.Abstractions;

namespace MetroToolKits.SectionGenerator.Infrastructure.Repositories;

/// <summary>
/// 基于 JSON 的墙体模板目录。
/// </summary>
public sealed class JsonWallAssemblyTemplateCatalog : IWallAssemblyTemplateCatalog
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
    private readonly ILogger<JsonWallAssemblyTemplateCatalog> _logger;
    private List<WallAssemblyTemplate>? _cache;
    private Dictionary<string, WallAssemblyTemplate>? _map;

    public JsonWallAssemblyTemplateCatalog(
        string configPath,
        string? templatePath,
        ILogger<JsonWallAssemblyTemplateCatalog> logger)
    {
        _configPath = configPath;
        _templatePath = templatePath;
        _logger = logger;
    }

    public IReadOnlyList<WallAssemblyTemplate> GetAllTemplates()
    {
        EnsureLoaded();
        return _cache!;
    }

    public WallAssemblyTemplate? GetById(string templateId)
    {
        EnsureLoaded();
        return string.IsNullOrWhiteSpace(templateId)
            ? null
            : _map!.TryGetValue(templateId, out var template)
                ? template
                : null;
    }

    public void SaveAll(IReadOnlyCollection<WallAssemblyTemplate> templates)
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

    private IReadOnlyList<WallAssemblyTemplate> LoadOrCreateTemplates()
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
            return JsonSerializer.Deserialize<List<WallAssemblyTemplate>>(json, ReadOptions)
                   ?? CreateDefaultTemplates();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "墙体模板配置解析失败，使用默认模板。");
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

    private static WallAssemblyTemplate CloneTemplate(WallAssemblyTemplate template)
    {
        return new WallAssemblyTemplate
        {
            TemplateId = template.TemplateId,
            TemplateName = template.TemplateName,
            VerticalAnchorMode = template.VerticalAnchorMode,
            CoreRule = new WallCoreRule
            {
                Name = template.CoreRule.Name,
                Thickness = template.CoreRule.Thickness,
                MaterialOrCategory = template.CoreRule.MaterialOrCategory,
                VisibleInSection = template.CoreRule.VisibleInSection,
                RecognitionMode = template.CoreRule.RecognitionMode
            },
            LeftLayers = template.LeftLayers
                .Select(CloneRule)
                .ToList(),
            RightLayers = template.RightLayers
                .Select(CloneRule)
                .ToList()
        };
    }

    private static WallLayerRule CloneRule(WallLayerRule rule)
    {
        return new WallLayerRule
        {
            Name = rule.Name,
            Side = rule.Side,
            Order = rule.Order,
            Thickness = rule.Thickness,
            MaterialOrCategory = rule.MaterialOrCategory,
            VisibleInSection = rule.VisibleInSection
        };
    }

    private static List<WallAssemblyTemplate> CreateDefaultTemplates()
    {
        return new List<WallAssemblyTemplate>
        {
            new()
            {
                TemplateId = "wall-200-finish",
                TemplateName = "200结构墙-双侧抹灰",
                VerticalAnchorMode = WallVerticalAnchorMode.StructuralSlabFaces,
                CoreRule = new WallCoreRule
                {
                    Name = "结构芯",
                    Thickness = 200,
                    MaterialOrCategory = "结构",
                    VisibleInSection = true,
                    RecognitionMode = WallCoreRecognitionMode.BoundaryPair
                },
                LeftLayers = new List<WallLayerRule>
                {
                    new()
                    {
                        Name = "左侧抹灰",
                        Side = WallLayerSide.Left,
                        Order = 1,
                        Thickness = 20,
                        MaterialOrCategory = "抹灰",
                        VisibleInSection = true
                    }
                },
                RightLayers = new List<WallLayerRule>
                {
                    new()
                    {
                        Name = "右侧抹灰",
                        Side = WallLayerSide.Right,
                        Order = 1,
                        Thickness = 20,
                        MaterialOrCategory = "抹灰",
                        VisibleInSection = true
                    }
                }
            }
        };
    }
}
