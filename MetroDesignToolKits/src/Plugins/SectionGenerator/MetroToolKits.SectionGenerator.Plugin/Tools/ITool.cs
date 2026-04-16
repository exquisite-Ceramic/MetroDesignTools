using System;
using System.Windows.Forms;

namespace SectionGenerator.Plugin.Tools;

/// <summary>
/// 工具接口 - MetroDesignTools 核心接口
/// </summary>
public interface ITool
{
    /// <summary>
    /// 工具唯一标识
    /// </summary>
    string Id { get; }

    /// <summary>
    /// 工具名称
    /// </summary>
    string Name { get; }

    /// <summary>
    /// 工具描述
    /// </summary>
    string Description { get; }

    /// <summary>
    /// 工具图标
    /// </summary>
    System.Drawing.Icon? Icon { get; }

    /// <summary>
    /// 工具分类
    /// </summary>
    string Category { get; }

    /// <summary>
    /// 是否启用
    /// </summary>
    bool IsEnabled { get; set; }

    /// <summary>
    /// 执行工具
    /// </summary>
    void Execute();

    /// <summary>
    /// 获取工具配置界面
    /// </summary>
    Control? GetConfigUI();

    /// <summary>
    /// 初始化工具
    /// </summary>
    void Initialize();

    /// <summary>
    /// 关闭工具
    /// </summary>
    void Shutdown();
}

/// <summary>
/// 工具元数据特性
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class ToolMetadataAttribute : Attribute
{
    public string Id { get; }
    public string Name { get; }
    public string Description { get; }
    public string Category { get; }
    public string? IconResource { get; }

    public ToolMetadataAttribute(string id, string name, string description, string category, string? iconResource = null)
    {
        Id = id;
        Name = name;
        Description = description;
        Category = category;
        IconResource = iconResource;
    }
}
