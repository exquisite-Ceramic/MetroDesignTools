using Autodesk.AutoCAD.ApplicationServices;
using Microsoft.Extensions.Logging;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace MetroToolKits.Bootstrap.Logging;

/// <summary>
/// 将日志输出到 AutoCAD 命令行的 Logger 实现
/// </summary>
internal sealed class AcadEditorLogger : ILogger
{
    private readonly string _categoryName;
    private readonly LogLevel _minLevel;

    public AcadEditorLogger(string categoryName, LogLevel minLevel)
    {
        _categoryName = categoryName;
        _minLevel = minLevel;
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => logLevel >= _minLevel;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
        Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel)) return;

        var doc = AcadApp.DocumentManager.MdiActiveDocument;
        if (doc == null) return;

        var message = formatter(state, exception);
        var prefix = logLevel switch
        {
            LogLevel.Warning  => "[警告] ",
            LogLevel.Error    => "[错误] ",
            LogLevel.Critical => "[严重] ",
            _                 => ""
        };

        doc.Editor.WriteMessage($"\n{prefix}{message}");

        if (exception != null && logLevel >= LogLevel.Error)
            doc.Editor.WriteMessage($"\n  原因: {exception.Message}");
    }
}

/// <summary>
/// AcadEditorLogger 提供者
/// </summary>
public sealed class AcadEditorLoggerProvider : ILoggerProvider
{
    private readonly LogLevel _minLevel;

    public AcadEditorLoggerProvider(LogLevel minLevel = LogLevel.Warning)
    {
        _minLevel = minLevel;
    }

    public ILogger CreateLogger(string categoryName)
        => new AcadEditorLogger(categoryName, _minLevel);

    public void Dispose() { }
}
