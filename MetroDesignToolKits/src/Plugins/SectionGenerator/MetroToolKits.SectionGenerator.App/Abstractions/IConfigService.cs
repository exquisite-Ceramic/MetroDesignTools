namespace SectionGenerator.App.Abstractions;

/// <summary>
/// 配置服务接口 - 用于读写应用程序配置
/// </summary>
public interface IConfigService
{
    /// <summary>
    /// 加载配置，如果不存在则创建默认配置
    /// </summary>
    /// <typeparam name="T">配置类型</typeparam>
    /// <param name="filePath">配置文件路径</param>
    /// <param name="defaultValue">默认值</param>
    /// <returns>配置对象</returns>
    T LoadOrCreate<T>(string filePath, T defaultValue) where T : class;

    /// <summary>
    /// 保存配置
    /// </summary>
    /// <typeparam name="T">配置类型</typeparam>
    /// <param name="filePath">配置文件路径</param>
    /// <param name="value">配置对象</param>
    void Save<T>(string filePath, T value);
}
