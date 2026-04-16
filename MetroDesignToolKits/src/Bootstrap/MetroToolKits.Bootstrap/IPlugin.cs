using Microsoft.Extensions.DependencyInjection;

namespace MetroToolKits.Bootstrap;

/// <summary>
/// 插件接口 - 所有工具插件必须实现此接口
/// </summary>
public interface IPlugin
{
    /// <summary>
    /// 插件名称
    /// </summary>
    string Name { get; }

    /// <summary>
    /// 插件版本
    /// </summary>
    string Version { get; }

    /// <summary>
    /// 配置服务 - 注册插件所需的服务
    /// </summary>
    void ConfigureServices(IServiceCollection services);

    /// <summary>
    /// 注册命令
    /// </summary>
    void RegisterCommands(ICommandRegistry registry);
}

/// <summary>
/// 命令注册接口
/// </summary>
public interface ICommandRegistry
{
    void RegisterCommand<T>(string commandName) where T : class;
}
