using Microsoft.Extensions.DependencyInjection;
using MetroToolKits.Foundation.Core.Hosting;

namespace MetroToolKits.Bootstrap;

/// <summary>
/// 命令注册器 - 将插件命令注册到 AutoCAD
/// </summary>
public class CommandRegistry : ICommandRegistry
{
    private readonly IServiceProvider _serviceProvider;
    private readonly Dictionary<string, CommandRegistration> _commands = new();

    public CommandRegistry(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    /// <summary>
    /// 注册命令
    /// </summary>
    public void RegisterCommand(Type commandType, string commandName, string methodName = "Execute")
    {
        _commands[commandName.ToUpperInvariant()] = new CommandRegistration(commandType, methodName);
    }

    /// <summary>
    /// 注册命令
    /// </summary>
    public void RegisterCommand<T>(string commandName, string methodName = "Execute") where T : class
    {
        RegisterCommand(typeof(T), commandName, methodName);
    }

    /// <summary>
    /// 获取命令注册信息
    /// </summary>
    public CommandRegistration? GetCommandRegistration(string commandName)
    {
        return _commands.TryGetValue(commandName.ToUpperInvariant(), out var command)
            ? command
            : null;
    }

    /// <summary>
    /// 获取命令实例
    /// </summary>
    public object? GetCommandInstance(CommandRegistration registration)
    {
        if (registration == null)
        {
            return null;
        }

        return _serviceProvider.GetService(registration.CommandType)
            ?? ActivatorUtilities.CreateInstance(_serviceProvider, registration.CommandType);
    }

    /// <summary>
    /// 获取所有已注册命令
    /// </summary>
    public IReadOnlyDictionary<string, CommandRegistration> Commands => _commands;
}

/// <summary>
/// 命令注册项。
/// </summary>
public sealed record CommandRegistration(Type CommandType, string MethodName);
