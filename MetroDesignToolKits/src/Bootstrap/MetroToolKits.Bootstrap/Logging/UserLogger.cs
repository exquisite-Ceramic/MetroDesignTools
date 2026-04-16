using Autodesk.AutoCAD.ApplicationServices;
using Newtonsoft.Json;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace MetroToolKits.Bootstrap.Logging;

/// <summary>
/// 用户日志服务 - 面向最终用户的命令行提示 + 用户日志文件
/// </summary>
public sealed class UserLogger
{
    private readonly FileLoggerProcessor? _fileProcessor;
    private readonly UserLogVerbosity _verbosity;

    public UserLogger(string? userLogFilePath = null, UserLogVerbosity verbosity = UserLogVerbosity.Normal)
    {
        _verbosity = verbosity;
        if (!string.IsNullOrEmpty(userLogFilePath))
            _fileProcessor = new FileLoggerProcessor(userLogFilePath);
    }

    // ── 命令生命周期 ──────────────────────────────────────────────────────────

    public void CommandStarted(string commandName)
    {
        WriteToCommandLine($"命令: {commandName} 已启动");
        WriteToFile("INFO", $"{commandName} started");
    }

    public void CommandCompleted(string commandName, string? detail = null)
    {
        var msg = detail ?? $"{commandName} 已完成";
        WriteToCommandLine(msg);
        WriteToFile("INFO", $"{commandName} completed", new { detail });
    }

    public void CommandCancelled(string commandName)
    {
        WriteToCommandLine("命令已取消");
        WriteToFile("INFO", $"{commandName} cancelled by user");
    }

    public void CommandFailed(string commandName, string userMessage, string? technicalDetail = null)
    {
        WriteToCommandLine($"[错误] {userMessage}");
        WriteToFile("ERROR", $"{commandName} failed: {technicalDetail ?? userMessage}");
    }

    // ── 剖面生成 ──────────────────────────────────────────────────────────────

    public void SectionGenerating(string floorName)
    {
        if (_verbosity >= UserLogVerbosity.Verbose)
            WriteToCommandLine($"正在生成剖面，处理楼层 {floorName}...");
        WriteToFile("INFO", $"Generating section for floor {floorName}");
    }

    public void SectionProgress(string floorName, int current, int total)
    {
        if (_verbosity >= UserLogVerbosity.Normal)
            WriteToCommandLine($"已完成 {floorName}... ({current}/{total})");
    }

    public void SectionCreated(string blockName, int floorCount, long elapsedMs)
    {
        WriteToCommandLine($"剖面生成成功。块名: {blockName}");
        WriteToFile("INFO", "Section block created",
            new { block = blockName, floors = floorCount, elapsedMs });
    }

    public void SectionLineInvalid()
    {
        WriteToCommandLine("[错误] 剖切线未穿过任何构件，请调整剖切线位置");
        WriteToFile("ERROR", "Section line does not intersect any element");
    }

    public void FloorSkipped(string floorName, string reason = "未识别到任何构件")
    {
        WriteToCommandLine($"[警告] 楼层 {floorName} {reason}，已跳过");
        WriteToFile("WARN", $"Floor {floorName} has no elements, skipped");
    }

    // ── 构件转换 ──────────────────────────────────────────────────────────────

    public void LayerMappingApplied(int entityCount)
    {
        WriteToCommandLine($"已转换 {entityCount} 个图元到标准图层");
        WriteToFile("INFO", "Layer mapping applied", new { entityCount });
    }

    public void RegionConversionStarted(int layerCount)
    {
        WriteToCommandLine($"区域转换模式，框选范围内共 {layerCount} 个图层");
        WriteToFile("INFO", "Region conversion started", new { layerCount });
    }

    public void ConversionApplied(int layerCount, int entityCount)
    {
        WriteToCommandLine($"转换完成，{layerCount} 个图层共 {entityCount} 个图元已更新");
        WriteToFile("INFO", "Conversion applied", new { layerCount, entityCount });
    }

    public void ConversionDiscarded()
    {
        WriteToCommandLine("转换已放弃");
        WriteToFile("INFO", "Conversion discarded by user");
    }

    public void RevertCompleted(int restoredCount)
    {
        WriteToCommandLine($"已恢复 {restoredCount} 个图元到原始图层");
        WriteToFile("INFO", "Revert completed", new { restoredCount });
    }

    // ── 楼层配置 ──────────────────────────────────────────────────────────────

    public void FloorConfigLoaded(int floorCount, string[] floorNames)
    {
        if (_verbosity >= UserLogVerbosity.Verbose)
            WriteToCommandLine($"已加载楼层配置: {floorCount} 个楼层 ({string.Join(",", floorNames)})");
        WriteToFile("INFO", "Floor config loaded", new { floorCount });
    }

    public void FloorConfigMissing()
    {
        WriteToCommandLine("[警告] 未找到楼层配置文件，将使用默认单层配置");
        WriteToFile("WARN", "Config file missing, using defaults");
    }

    public void AlignmentPointsSet(string floorName)
    {
        WriteToCommandLine($"楼层 {floorName} 对齐点已记录");
        WriteToFile("INFO", $"Alignment points set for floor {floorName}");
    }

    // ── 变更检测 ──────────────────────────────────────────────────────────────

    public void CheckingUpdates()
    {
        WriteToCommandLine("正在检查剖面更新状态...");
        WriteToFile("INFO", "Checking section updates");
    }

    public void CheckResult(int total, int outdated)
    {
        WriteToCommandLine($"共 {total} 个剖面，其中 {outdated} 个需要更新");
        WriteToFile("INFO", "Sections checked", new { total, outdated });
    }

    public void BatchUpdateStarted(int count)
    {
        WriteToCommandLine($"正在更新 {count} 个剖面...");
        WriteToFile("INFO", "Batch updating sections", new { count });
    }

    public void BatchUpdateCompleted(int success, int failed)
    {
        WriteToCommandLine($"更新完成：成功 {success} 个，失败 {failed} 个");
        WriteToFile("INFO", "Batch update done", new { success, failed });
    }

    public void SectionUpdated(string blockName)
    {
        WriteToCommandLine($"剖面 [{blockName}] 已更新");
        WriteToFile("INFO", "Section updated", new { block = blockName });
    }

    public void SectionUpdateFailed(string blockName, string reason)
    {
        WriteToCommandLine($"[错误] 剖面 [{blockName}] 更新失败：{reason}");
        WriteToFile("ERROR", $"Section update failed: {reason}", new { block = blockName });
    }

    // ── 内部辅助 ──────────────────────────────────────────────────────────────

    private static void WriteToCommandLine(string message)
    {
        var doc = AcadApp.DocumentManager.MdiActiveDocument;
        doc?.Editor.WriteMessage($"\n{message}");
    }

    private void WriteToFile(string level, string msg, object? extra = null)
    {
        if (_fileProcessor == null) return;

        var entry = new Dictionary<string, object?>
        {
            ["time"]  = DateTime.Now.ToString("O"),
            ["level"] = level,
            ["msg"]   = msg,
            ["user"]  = Environment.UserName
        };

        if (extra != null)
        {
            foreach (var prop in extra.GetType().GetProperties())
                entry[prop.Name] = prop.GetValue(extra);
        }

        _fileProcessor.Enqueue(JsonConvert.SerializeObject(entry));
    }
}

/// <summary>
/// 用户日志详细程度
/// </summary>
public enum UserLogVerbosity
{
    ErrorOnly = 0,
    Normal    = 1,
    Verbose   = 2
}
