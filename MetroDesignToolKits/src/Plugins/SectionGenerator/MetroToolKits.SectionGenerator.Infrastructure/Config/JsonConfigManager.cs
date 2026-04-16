using System.IO;
using Newtonsoft.Json;
using SectionGenerator.App.Abstractions;

namespace SectionGenerator.Infrastructure.Config;

/// <summary>
/// JSON 配置管理器 - 实现 IConfigService 接口
/// </summary>
public sealed class JsonConfigManager : IConfigService
{
    public T LoadOrCreate<T>(string filePath, T defaultValue) where T : class
    {
        if (File.Exists(filePath))
        {
            try
            {
                string json = File.ReadAllText(filePath);
                return JsonConvert.DeserializeObject<T>(json) ?? defaultValue;
            }
            catch
            {
                return defaultValue;
            }
        }

        Save(filePath, defaultValue);
        return defaultValue;
    }

    public void Save<T>(string filePath, T value)
    {
        string? dir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrWhiteSpace(dir))
            Directory.CreateDirectory(dir);

        string json = JsonConvert.SerializeObject(value, Formatting.Indented);
        File.WriteAllText(filePath, json);
    }
}
