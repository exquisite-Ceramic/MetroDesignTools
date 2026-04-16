using MetroToolKits.Foundation.Building.Types;

namespace MetroToolKits.SectionGenerator.Plugin.Services;

/// <summary>
/// 构件类型服务 - 提供类型查询和快捷键映射
/// </summary>
public sealed class ElementTypeService
{
    private readonly ElementTypeLoader _loader;
    private List<ElementTypeDefinition>? _cachedTypes;
    private Dictionary<string, ElementTypeDefinition>? _shortcutMap;
    private Dictionary<string, ElementTypeDefinition>? _typeIdMap;

    public ElementTypeService(ElementTypeLoader loader)
    {
        _loader = loader;
    }

    /// <summary>
    /// 获取所有构件类型
    /// </summary>
    public IReadOnlyList<ElementTypeDefinition> GetAllTypes()
    {
        EnsureLoaded();
        return _cachedTypes!;
    }

    /// <summary>
    /// 获取启用的构件类型
    /// </summary>
    public IReadOnlyList<ElementTypeDefinition> GetEnabledTypes()
    {
        return GetAllTypes().Where(t => t.IsEnabled).ToList();
    }

    /// <summary>
    /// 根据快捷键获取类型
    /// </summary>
    public ElementTypeDefinition? GetByShortcutKey(string shortcutKey)
    {
        EnsureLoaded();
        return _shortcutMap!.TryGetValue(shortcutKey.ToUpper(), out var type) ? type : null;
    }

    /// <summary>
    /// 根据类型ID获取类型
    /// </summary>
    public ElementTypeDefinition? GetByTypeId(string typeId)
    {
        EnsureLoaded();
        return _typeIdMap!.TryGetValue(typeId, out var type) ? type : null;
    }

    /// <summary>
    /// 生成命令行提示文本
    /// </summary>
    public string GenerateCommandLinePrompt()
    {
        var types = GetEnabledTypes();
        var hints = types.Select(t => $"{t.TypeName}({t.ShortcutKey})");
        return string.Join(" | ", hints);
    }

    /// <summary>
    /// 生成类型选择菜单
    /// </summary>
    public string GenerateTypeMenu()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("可用构件类型:");
        sb.AppendLine(new string('-', 30));

        foreach (var type in GetEnabledTypes())
        {
            sb.AppendLine($"  [{type.ShortcutKey}] {type.TypeName}");
        }

        sb.AppendLine(new string('-', 30));
        sb.AppendLine("  [ESC] 跳过");
        sb.AppendLine("  [Q] 退出");

        return sb.ToString();
    }

    /// <summary>
    /// 验证快捷键是否有效
    /// </summary>
    public bool IsValidShortcutKey(string shortcutKey)
    {
        return GetByShortcutKey(shortcutKey) != null;
    }

    /// <summary>
    /// 重新加载类型配置
    /// </summary>
    public void Reload()
    {
        _cachedTypes = null;
        _shortcutMap = null;
        _typeIdMap = null;
        EnsureLoaded();
    }

    private void EnsureLoaded()
    {
        if (_cachedTypes != null) return;

        _cachedTypes = _loader.Load().ToList();
        _shortcutMap = _cachedTypes
            .Where(t => !string.IsNullOrEmpty(t.ShortcutKey))
            .ToDictionary(t => t.ShortcutKey.ToUpper(), t => t);
        _typeIdMap = _cachedTypes
            .Where(t => !string.IsNullOrEmpty(t.TypeId))
            .ToDictionary(t => t.TypeId, t => t);
    }
}
