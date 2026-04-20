using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MetroToolKits.Bootstrap.Logging;
using System.Reflection;

namespace MetroToolKits.Bootstrap;

/// <summary>
/// 应用程序启动类 - 初始化 DI 容器、日志系统和插件系统
/// </summary>
public static class Startup
{
    private static IServiceProvider? _serviceProvider;
    private static ILoggerFactory? _loggerFactory;
    private static UserLogger? _userLogger;
    private static bool _initialized;
    private static readonly HashSet<string> _requestedPluginKeys = new(StringComparer.OrdinalIgnoreCase);

    public static void Initialize(params Assembly[] additionalPluginAssemblies)
    {
        var additionalKeys = additionalPluginAssemblies
            .Where(static assembly => assembly != null)
            .Select(GetAssemblyKey)
            .ToArray();

        var requiresReload = !_initialized
                             || additionalKeys.Any(key => !_requestedPluginKeys.Contains(key));

        if (!requiresReload) return;

        try
        {
            var assemblyDir = Path.GetDirectoryName(typeof(Startup).Assembly.Location)
                              ?? AppDomain.CurrentDomain.BaseDirectory;

            // 1. 加载配置
            var config = BuildConfiguration(assemblyDir);

            // 2. 构建日志工厂
            _loggerFactory = BuildLoggerFactory(config, assemblyDir);
            var logger = _loggerFactory.CreateLogger("MetroToolKits.Bootstrap.Startup");

            logger.LogDebug("DI 容器初始化开始");
            var sw = System.Diagnostics.Stopwatch.StartNew();

            // 3. 构建用户日志
            var userLogPath = GetAbsolutePath(
                config["Logging:Outputs:File:UserLogPath"] ?? "logs/MetroToolKits_User.log",
                assemblyDir);
            var verbosity = Enum.TryParse<UserLogVerbosity>(
                config["UserLogging:Verbosity"] ?? "Normal", true, out var v) ? v : UserLogVerbosity.Normal;
            _userLogger = new UserLogger(userLogPath, verbosity);

            // 4. 注册服务
            var services = new ServiceCollection();
            services.AddSingleton(_loggerFactory);
            // UserLogger 同时作为 IUserLogger 接口注册，供所有依赖 IUserLogger 的用例解析
            services.AddSingleton<MetroToolKits.Foundation.Core.Logging.IUserLogger>(_userLogger);
            services.AddSingleton(typeof(ILogger<>), typeof(Logger<>));

            // 5. 加载插件
            var pluginLoader = new PluginLoader(services);
            pluginLoader.LoadPlugins(assemblyDir);
            foreach (var assembly in additionalPluginAssemblies)
                pluginLoader.LoadPlugin(assembly);

            _serviceProvider = services.BuildServiceProvider();

            // 6. 注册命令
            var commandRegistry = new CommandRegistry(_serviceProvider);
            foreach (var plugin in pluginLoader.Plugins)
                plugin.RegisterCommands(commandRegistry);

            sw.Stop();
            logger.LogDebug("DI 容器初始化完成，耗时 {ElapsedMs}ms", sw.ElapsedMilliseconds);
            logger.LogInformation("MetroToolKits Bootstrap 已加载，共 {PluginCount} 个插件",
                pluginLoader.Plugins.Count);

            _requestedPluginKeys.Clear();
            foreach (var key in additionalKeys)
                _requestedPluginKeys.Add(key);

            _initialized = true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Bootstrap 初始化失败: {ex}");
            _loggerFactory?.CreateLogger("Bootstrap")
                .LogError(ex, "Bootstrap 初始化失败");
        }
    }

    public static IServiceProvider? ServiceProvider => _serviceProvider;
    public static ILoggerFactory? LoggerFactory => _loggerFactory;
    public static UserLogger? UserLogger => _userLogger;

    // ── 私有辅助 ──────────────────────────────────────────────────────────────

    private static IConfiguration BuildConfiguration(string baseDir)
    {
        var env = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "Production";
        return new ConfigurationBuilder()
            .SetBasePath(baseDir)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
            .AddJsonFile($"appsettings.{env}.json", optional: true, reloadOnChange: false)
            .AddEnvironmentVariables()
            .Build();
    }

    private static ILoggerFactory BuildLoggerFactory(IConfiguration config, string baseDir)
    {
        var defaultLevel = Enum.TryParse<LogLevel>(
            config["Logging:LogLevel:Default"] ?? "Information", true, out var dl)
            ? dl : LogLevel.Information;

        var acadMinLevel = Enum.TryParse<LogLevel>(
            config["Logging:Outputs:AcadCommandLine:MinLevel"] ?? "Warning", true, out var al)
            ? al : LogLevel.Warning;

        var fileEnabled = config["Logging:Outputs:File:Enabled"]?.ToLower() != "false";
        var filePath = GetAbsolutePath(
            config["Logging:Outputs:File:Path"] ?? "logs/MetroToolKits.log", baseDir);

        return Microsoft.Extensions.Logging.LoggerFactory.Create(builder =>
        {
            builder.SetMinimumLevel(defaultLevel);

            // AutoCAD 命令行输出
            builder.AddProvider(new AcadEditorLoggerProvider(acadMinLevel));

            // 文件输出
            if (fileEnabled)
                builder.AddProvider(new FileLoggerProvider(filePath));
        });
    }

    private static string GetAbsolutePath(string path, string baseDir)
        => Path.IsPathRooted(path) ? path : Path.Combine(baseDir, path);

    private static string GetAssemblyKey(Assembly assembly)
        => string.IsNullOrWhiteSpace(assembly.Location)
            ? assembly.FullName ?? assembly.GetName().Name ?? Guid.NewGuid().ToString("N")
            : Path.GetFullPath(assembly.Location);
}
