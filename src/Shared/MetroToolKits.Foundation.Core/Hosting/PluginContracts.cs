using Microsoft.Extensions.DependencyInjection;

namespace MetroToolKits.Foundation.Core.Hosting;

/// <summary>
/// 插件接口 - 所有工具插件必须实现此接口
/// </summary>
public interface IPlugin
{
    string Name { get; }

    string Version { get; }

    void ConfigureServices(IServiceCollection services);

    void RegisterCommands(ICommandRegistry registry);
}

/// <summary>
/// 命令注册接口
/// </summary>
public interface ICommandRegistry
{
    void RegisterCommand(Type commandType, string commandName, string methodName = "Execute");

    void RegisterCommand<T>(string commandName, string methodName = "Execute") where T : class;
}
