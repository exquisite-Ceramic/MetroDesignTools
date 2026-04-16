using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;

namespace SectionGenerator.Plugin.Tools;

/// <summary>
/// 工具管理器 - MetroDesignTools 核心
/// </summary>
public sealed class ToolManager
{
    private static ToolManager? _instance;
    private static readonly object _lock = new();
    
    private readonly Dictionary<string, ITool> _tools = new();
    private readonly Dictionary<string, List<ITool>> _categories = new();
    private bool _isInitialized = false;

    /// <summary>
    /// 单例实例
    /// </summary>
    public static ToolManager Instance
    {
        get
        {
            if (_instance == null)
            {
                lock (_lock)
                {
                    _instance ??= new ToolManager();
                }
            }
            return _instance;
        }
    }

    /// <summary>
    /// 工具集合
    /// </summary>
    public IReadOnlyCollection<ITool> Tools => _tools.Values;

    /// <summary>
    /// 分类集合
    /// </summary>
    public IReadOnlyCollection<string> Categories => _categories.Keys;

    /// <summary>
    /// 初始化工具管理器
    /// </summary>
    public void Initialize()
    {
        if (_isInitialized) return;

        // 自动发现和注册工具
        DiscoverAndRegisterTools();
        
        _isInitialized = true;
    }

    /// <summary>
    /// 关闭工具管理器
    /// </summary>
    public void Shutdown()
    {
        foreach (var tool in _tools.Values)
        {
            tool.Shutdown();
        }
        _tools.Clear();
        _categories.Clear();
        _isInitialized = false;
    }

    /// <summary>
    /// 注册工具
    /// </summary>
    public void RegisterTool(ITool tool)
    {
        if (_tools.ContainsKey(tool.Id))
        {
            throw new InvalidOperationException($"工具 '{tool.Id}' 已注册");
        }

        _tools[tool.Id] = tool;
        
        // 按分类组织
        if (!_categories.ContainsKey(tool.Category))
        {
            _categories[tool.Category] = new List<ITool>();
        }
        _categories[tool.Category].Add(tool);

        // 初始化工具
        tool.Initialize();
    }

    /// <summary>
    /// 获取工具
    /// </summary>
    public ITool? GetTool(string id)
    {
        return _tools.TryGetValue(id, out var tool) ? tool : null;
    }

    /// <summary>
    /// 获取分类下的工具
    /// </summary>
    public IReadOnlyList<ITool> GetToolsByCategory(string category)
    {
        return _categories.TryGetValue(category, out var tools) 
            ? tools.AsReadOnly() 
            : new List<ITool>().AsReadOnly();
    }

    /// <summary>
    /// 执行工具
    /// </summary>
    public void ExecuteTool(string id)
    {
        var tool = GetTool(id);
        if (tool != null)
        {
            tool.Execute();
        }
        else
        {
            MessageBox.Show($"未找到工具: {id}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    /// <summary>
    /// 自动发现工具
    /// </summary>
    private void DiscoverAndRegisterTools()
    {
        var toolTypes = Assembly.GetExecutingAssembly()
            .GetTypes()
            .Where(t => typeof(ITool).IsAssignableFrom(t) 
                && !t.IsInterface 
                && !t.IsAbstract);

        foreach (var type in toolTypes)
        {
            try
            {
                var tool = (ITool?)Activator.CreateInstance(type);
                if (tool != null)
                {
                    RegisterTool(tool);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"注册工具失败 {type.Name}: {ex.Message}");
            }
        }
    }
}
