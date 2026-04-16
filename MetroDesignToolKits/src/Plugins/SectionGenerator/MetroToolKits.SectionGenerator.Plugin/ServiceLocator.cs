using System;
using System.Collections.Generic;
using SectionGenerator.App.Abstractions;
using SectionGenerator.Core.Sections;
using SectionGenerator.Infrastructure.Config;

namespace SectionGenerator;

/// <summary>
/// 服务定位器 - 用于管理依赖注入
/// 注意：在 AutoCAD 插件环境中使用简单的服务定位器模式
/// </summary>
public static class ServiceLocator
{
    private static readonly Dictionary<Type, object> _services = new();
    private static readonly object _lock = new();

    /// <summary>
    /// 初始化服务定位器
    /// </summary>
    public static void Initialize()
    {
        RegisterServices();
    }

    /// <summary>
    /// 注册所有服务
    /// </summary>
    private static void RegisterServices()
    {
        // 注册基础设施层服务
        Register<IConfigService>(new JsonConfigManager());
        
        // 注册层构建器配置（使用标准配置）
        Register<IEnumerable<ILayerBuilder>>(LayerConfigurations.Standard);
        
        // 注册领域层服务
        Register<ISectionGenerator>(new Core.Sections.SectionGenerator(GetService<IEnumerable<ILayerBuilder>>()));
    }

    /// <summary>
    /// 注册服务
    /// </summary>
    /// <typeparam name="T">服务接口类型</typeparam>
    /// <param name="implementation">服务实现</param>
    public static void Register<T>(T implementation) where T : class
    {
        lock (_lock)
        {
            _services[typeof(T)] = implementation ?? throw new ArgumentNullException(nameof(implementation));
        }
    }

    /// <summary>
    /// 获取服务
    /// </summary>
    /// <typeparam name="T">服务接口类型</typeparam>
    /// <returns>服务实现</returns>
    /// <exception cref="InvalidOperationException">服务未注册时抛出</exception>
    public static T GetService<T>() where T : class
    {
        lock (_lock)
        {
            if (_services.TryGetValue(typeof(T), out var service))
            {
                return (T)service;
            }

            // 如果服务未注册，尝试初始化
            if (_services.Count == 0)
            {
                Initialize();
                if (_services.TryGetValue(typeof(T), out var initializedService))
                {
                    return (T)initializedService;
                }
            }

            throw new InvalidOperationException($"服务 {typeof(T).Name} 未注册");
        }
    }

    /// <summary>
    /// 检查服务是否已注册
    /// </summary>
    /// <typeparam name="T">服务接口类型</typeparam>
    /// <returns>是否已注册</returns>
    public static bool IsRegistered<T>() where T : class
    {
        lock (_lock)
        {
            return _services.ContainsKey(typeof(T));
        }
    }

    /// <summary>
    /// 清除所有服务（主要用于测试）
    /// </summary>
    public static void Clear()
    {
        lock (_lock)
        {
            _services.Clear();
        }
    }
}
