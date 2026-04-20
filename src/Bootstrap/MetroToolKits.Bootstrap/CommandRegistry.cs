using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace MetroToolKits.Bootstrap;

/// <summary>
/// 命令注册器 - 将插件命令注册到 AutoCAD
/// </summary>
public class CommandRegistry : ICommandRegistry
{
    private readonly IServiceProvider _serviceProvider;
    private readonly Dictionary<string, Type> _commands = new();

    public CommandRegistry(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    /// <summary>
    /// 注册命令
    /// </summary>
    public void RegisterCommand<T>(string commandName) where T : class
    {
        _commands[commandName.ToUpper()] = typeof(T);
    }

    /// <summary>
    /// 获取命令实例
    /// </summary>
    public object? GetCommandInstance(string commandName)
    {
        if (_commands.TryGetValue(commandName.ToUpper(), out var commandType))
        {
            return _serviceProvider.GetService(commandType) 
                ?? ActivatorUtilities.CreateInstance(_serviceProvider, commandType);
        }
        return null;
    }

    /// <summary>
    /// 获取所有已注册命令
    /// </summary>
    public IReadOnlyDictionary<string, Type> Commands => _commands;
}

/// <summary>
/// 命令基类 - 所有插件命令应继承此类
/// </summary>
public abstract class CommandBase
{
    /// <summary>
    /// 执行命令
    /// </summary>
    public abstract void Execute();
}

/// <summary>
/// 命令方法特性 - 标记命令入口
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public class CommandMethodAttribute : Attribute
{
    public string CommandName { get; }
    public string? GroupName { get; set; }

    public CommandMethodAttribute(string commandName)
    {
        CommandName = commandName;
    }
}
