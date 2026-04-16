using Microsoft.Extensions.Logging;

namespace MetroToolKits.Bootstrap.Logging;

/// <summary>
/// 日志配置（从 appsettings.json 反序列化）
/// </summary>
public sealed class LoggingConfig
{
    public LogLevelConfig LogLevel { get; set; } = new();
    public OutputsConfig Outputs { get; set; } = new();
}

public sealed class LogLevelConfig
{
    public string Default { get; set; } = "Information";
    public Dictionary<string, string> Categories { get; set; } = new();

    public LogLevel GetDefaultLevel()
        => Enum.TryParse<LogLevel>(Default, true, out var l) ? l : LogLevel.Information;

    public LogLevel GetLevelForCategory(string category)
    {
        foreach (var kv in Categories)
            if (category.StartsWith(kv.Key, StringComparison.OrdinalIgnoreCase))
                if (Enum.TryParse<LogLevel>(kv.Value, true, out var l)) return l;
        return GetDefaultLevel();
    }
}

public sealed class OutputsConfig
{
    public FileOutputConfig File { get; set; } = new();
    public AcadOutputConfig AcadCommandLine { get; set; } = new();
}

public sealed class FileOutputConfig
{
    public bool Enabled { get; set; } = true;
    public string Path { get; set; } = "logs/MetroToolKits.log";
    public string UserLogPath { get; set; } = "logs/MetroToolKits_User.log";
    public int MaxFileSizeMB { get; set; } = 10;
}

public sealed class AcadOutputConfig
{
    public bool Enabled { get; set; } = true;
    public string MinLevel { get; set; } = "Warning";

    public LogLevel GetMinLevel()
        => Enum.TryParse<LogLevel>(MinLevel, true, out var l) ? l : LogLevel.Warning;
}

public sealed class UserLoggingConfig
{
    public string Verbosity { get; set; } = "Normal";

    public UserLogVerbosity GetVerbosity() => Verbosity.ToLower() switch
    {
        "verbose" => UserLogVerbosity.Verbose,
        "erroronly" => UserLogVerbosity.ErrorOnly,
        _ => UserLogVerbosity.Normal
    };
}
