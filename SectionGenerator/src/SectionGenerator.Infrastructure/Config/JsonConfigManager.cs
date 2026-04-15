using System.IO;
using Newtonsoft.Json;

namespace SectionGenerator.Infrastructure.Config;

public sealed class JsonConfigManager
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

