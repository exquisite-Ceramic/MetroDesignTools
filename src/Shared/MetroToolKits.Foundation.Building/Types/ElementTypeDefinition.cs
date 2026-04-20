namespace MetroToolKits.Foundation.Building.Types;

/// <summary>
/// 构件类型定义
/// </summary>
public sealed class ElementTypeDefinition
{
    /// <summary>
    /// 类型标识
    /// </summary>
    public string TypeId { get; set; } = string.Empty;

    /// <summary>
    /// 类型名称
    /// </summary>
    public string TypeName { get; set; } = string.Empty;

    /// <summary>
    /// 快捷键字母
    /// </summary>
    public string ShortcutKey { get; set; } = string.Empty;

    /// <summary>
    /// 目标图层前缀
    /// </summary>
    public string TargetLayerPrefix { get; set; } = "MK_";

    /// <summary>
    /// 图层颜色索引
    /// </summary>
    public short LayerColorIndex { get; set; } = 7;

    /// <summary>
    /// 线型名称
    /// </summary>
    public string LinetypeName { get; set; } = "Continuous";

    /// <summary>
    /// 是否启用
    /// </summary>
    public bool IsEnabled { get; set; } = true;
}
