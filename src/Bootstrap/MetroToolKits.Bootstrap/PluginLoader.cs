using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using MetroToolKits.Foundation.Core.Hosting;

namespace MetroToolKits.Bootstrap;

/// <summary>
/// 插件加载器 - 扫描并加载所有插件
/// </summary>
public class PluginLoader
{
    private readonly List<IPlugin> _plugins = new();
    private readonly HashSet<string> _loadedPluginKeys = new(StringComparer.OrdinalIgnoreCase);
    private readonly IServiceCollection _services;

    public PluginLoader(IServiceCollection services)
    {
        _services = services;
    }

    /// <summary>
    /// 从指定目录加载插件
    /// </summary>
    public void LoadPlugins(string directory)
    {
        if (!Directory.Exists(directory))
            return;

        var pluginFiles = Directory.GetFiles(directory, "MetroToolKits.*.Plugin.dll");

        foreach (var file in pluginFiles)
        {
            try
            {
                LoadPlugin(file);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"加载插件失败 {file}: {ex.Message}");
            }
        }
    }

    public void LoadPlugin(Assembly assembly)
    {
        var key = GetPluginKey(assembly);
        if (!_loadedPluginKeys.Add(key))
            return;

        var pluginTypes = assembly.GetTypes()
            .Where(t => typeof(IPlugin).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);

        foreach (var type in pluginTypes)
        {
            if (Activator.CreateInstance(type) is IPlugin plugin)
            {
                _plugins.Add(plugin);
                plugin.ConfigureServices(_services);
            }
        }
    }

    private void LoadPlugin(string filePath)
    {
        var assembly = Assembly.LoadFrom(filePath);
        LoadPlugin(assembly);
    }

    public IReadOnlyList<IPlugin> Plugins => _plugins;

    private static string GetPluginKey(Assembly assembly)
        => string.IsNullOrWhiteSpace(assembly.Location)
            ? assembly.FullName ?? assembly.GetName().Name ?? Guid.NewGuid().ToString("N")
            : Path.GetFullPath(assembly.Location);
}
