using System.Text.Json;

namespace MetroToolKits.Foundation.Building.Types;

/// <summary>
/// 构件类型加载器
/// </summary>
public sealed class ElementTypeLoader
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
    private List<ElementTypeDefinition>? _types;

    public ElementTypeLoader(string configPath)
    {
        _configPath = configPath;
    }

    /// <summary>
    /// 加载构件类型配置
    /// </summary>
    public IReadOnlyList<ElementTypeDefinition> Load()
    {
        if (_types != null) return _types.AsReadOnly();

        if (!File.Exists(_configPath))
        {
            _types = CreateDefaultTypes();
            Save(_types);
            return _types.AsReadOnly();
        }

        try
        {
            var json = File.ReadAllText(_configPath);
            _types = JsonSerializer.Deserialize<List<ElementTypeDefinition>>(json, ReadOptions)
                ?? CreateDefaultTypes();
        }
        catch
        {
            _types = CreateDefaultTypes();
        }

        return _types.AsReadOnly();
    }

    /// <summary>
    /// 保存构件类型配置
    /// </summary>
    public void Save(IEnumerable<ElementTypeDefinition> types)
    {
        var directory = Path.GetDirectoryName(_configPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(types, WriteOptions);
        File.WriteAllText(_configPath, json);
    }

    /// <summary>
    /// 根据快捷键获取类型定义
    /// </summary>
    public ElementTypeDefinition? GetByShortcutKey(string shortcutKey)
    {
        return Load().FirstOrDefault(t => 
            t.ShortcutKey.Equals(shortcutKey, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// 根据类型ID获取类型定义
    /// </summary>
    public ElementTypeDefinition? GetByTypeId(string typeId)
    {
        return Load().FirstOrDefault(t => t.TypeId == typeId);
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
            new() { TypeId = "Stair", TypeName = "楼梯", ShortcutKey = "T", TargetLayerPrefix = "MK_楼梯", LayerColorIndex = 6 }
        };
    }
}
