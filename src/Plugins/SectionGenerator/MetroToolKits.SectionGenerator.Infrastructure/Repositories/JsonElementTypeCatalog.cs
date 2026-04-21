using System.Text.Json;
using Microsoft.Extensions.Logging;
using MetroToolKits.Foundation.Building.Types;
using MetroToolKits.SectionGenerator.App.Abstractions;

namespace MetroToolKits.SectionGenerator.Infrastructure.Repositories;

/// <summary>
/// 基于 JSON 文件的构件类型目录。
/// </summary>
public sealed class JsonElementTypeCatalog : IElementTypeCatalog
{
    private static readonly JsonSerializerOptions ReadOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _configPath;
    private readonly string? _templatePath;
    private readonly ILogger<JsonElementTypeCatalog> _logger;
    private List<ElementTypeDefinition>? _cachedTypes;
    private Dictionary<string, ElementTypeDefinition>? _shortcutMap;
    private Dictionary<string, ElementTypeDefinition>? _typeIdMap;

    public JsonElementTypeCatalog(
        string configPath,
        string? templatePath,
        ILogger<JsonElementTypeCatalog> logger)
    {
        _configPath = configPath;
        _templatePath = templatePath;
        _logger = logger;
    }

    public IReadOnlyList<ElementTypeDefinition> GetAllTypes()
    {
        EnsureLoaded();
        return _cachedTypes!;
    }

    public IReadOnlyList<ElementTypeDefinition> GetEnabledTypes()
        => GetAllTypes().Where(t => t.IsEnabled).ToList();

    public ElementTypeDefinition? GetByShortcutKey(string shortcutKey)
    {
        EnsureLoaded();
        return _shortcutMap!.TryGetValue(shortcutKey.ToUpperInvariant(), out var type) ? type : null;
    }

    public ElementTypeDefinition? GetByTypeId(string typeId)
    {
        EnsureLoaded();
        return _typeIdMap!.TryGetValue(typeId, out var type) ? type : null;
    }

    private void EnsureLoaded()
    {
        if (_cachedTypes != null)
        {
            return;
        }

        var types = LoadOrCreateTypes();
        _cachedTypes = types.ToList();
        _shortcutMap = _cachedTypes
            .Where(t => !string.IsNullOrWhiteSpace(t.ShortcutKey))
            .ToDictionary(t => t.ShortcutKey.ToUpperInvariant(), t => t);
        _typeIdMap = _cachedTypes
            .Where(t => !string.IsNullOrWhiteSpace(t.TypeId))
            .ToDictionary(t => t.TypeId, t => t);
    }

    private IReadOnlyList<ElementTypeDefinition> LoadOrCreateTypes()
    {
        if (!File.Exists(_configPath))
        {
            if (TrySeedFromTemplate())
            {
                _logger.LogInformation("构件类型配置不存在，已从模板复制到用户目录: {ConfigPath}", _configPath);
            }
            else
            {
                _logger.LogWarning("构件类型配置不存在，写入默认配置: {ConfigPath}", _configPath);
                var defaults = CreateDefaultTypes();
                Save(defaults);
                return defaults;
            }
        }

        try
        {
            var json = File.ReadAllText(_configPath);
            return JsonSerializer.Deserialize<List<ElementTypeDefinition>>(json, ReadOptions)
                ?? CreateDefaultTypes();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "构件类型配置解析失败，使用默认配置: {ConfigPath}", _configPath);
            return CreateDefaultTypes();
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

    private void Save(IEnumerable<ElementTypeDefinition> types)
    {
        var directory = Path.GetDirectoryName(_configPath);
        if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(types, WriteOptions);
        File.WriteAllText(_configPath, json);
    }

    private static List<ElementTypeDefinition> CreateDefaultTypes()
    {
        return new List<ElementTypeDefinition>
        {
            new() { TypeId = "Wall", TypeName = "结构墙", ShortcutKey = "W", TargetLayerPrefix = "MK_结构墙", LayerColorIndex = 1 },
            new() { TypeId = "Column", TypeName = "结构柱", ShortcutKey = "C", TargetLayerPrefix = "MK_结构柱", LayerColorIndex = 2 },
            new() { TypeId = "Slab", TypeName = "楼板", ShortcutKey = "S", TargetLayerPrefix = "MK_楼板", LayerColorIndex = 3 },
            new() { TypeId = "Door", TypeName = "门", ShortcutKey = "D", TargetLayerPrefix = "MK_门", LayerColorIndex = 4 },
            new() { TypeId = "Window", TypeName = "窗", ShortcutKey = "N", TargetLayerPrefix = "MK_窗", LayerColorIndex = 5 },
            new() { TypeId = "Stair", TypeName = "楼梯", ShortcutKey = "T", TargetLayerPrefix = "MK_楼梯", LayerColorIndex = 6 },
            new() { TypeId = "Beam", TypeName = "梁", ShortcutKey = "B", TargetLayerPrefix = "MK_梁", LayerColorIndex = 7 },
            new() { TypeId = "Opening", TypeName = "洞口", ShortcutKey = "O", TargetLayerPrefix = "MK_洞口", LayerColorIndex = 8 }
        };
    }
}
