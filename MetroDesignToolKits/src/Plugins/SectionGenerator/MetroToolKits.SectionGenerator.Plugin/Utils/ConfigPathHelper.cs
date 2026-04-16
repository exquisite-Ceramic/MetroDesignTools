using System.IO;
using System.Reflection;

namespace SectionGenerator.Plugin.Utils;

/// <summary>
/// 配置文件路径辅助类
/// </summary>
public static class ConfigPathHelper
{
    /// <summary>
    /// 获取配置文件完整路径
    /// </summary>
    public static string GetConfigPath(string fileName)
    {
        string? assemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        return Path.Combine(assemblyDir ?? string.Empty, fileName);
    }
}
