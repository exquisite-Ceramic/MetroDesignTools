namespace MetroToolKits.Foundation.Core.Logging;

/// <summary>
/// 用户日志接口 - 向用户输出命令行提示，不依赖任何具体实现
/// 定义在 Foundation.Core，供 App 层和 Bootstrap 层共同使用
/// </summary>
public interface IUserLogger
{
    // ── 命令生命周期 ──────────────────────────────────────────────────────────
    void CommandStarted(string commandName);
    void CommandCompleted(string commandName, string? detail = null);
    void CommandCancelled(string commandName);
    void CommandFailed(string commandName, string userMessage, string? technicalDetail = null);

    // ── 剖面生成 ──────────────────────────────────────────────────────────────
    void SectionGenerating(string floorName);
    void SectionProgress(string floorName, int current, int total);
    void SectionCreated(string blockName, int floorCount, long elapsedMs);
    void SectionLineInvalid();
    void FloorSkipped(string floorName, string reason = "未识别到任何构件");

    // ── 楼层配置 ──────────────────────────────────────────────────────────────
    void FloorConfigLoaded(int floorCount, string[] floorNames);
    void FloorConfigSaved(int floorCount, string[] floorNames);
    void FloorConfigMissing();
    void AlignmentPointsSet(string floorName);

    // ── 变更检测 ──────────────────────────────────────────────────────────────
    void CheckingUpdates();
    void CheckResult(int total, int outdated);
    void BatchUpdateStarted(int count);
    void BatchUpdateCompleted(int success, int failed);
    void SectionUpdated(string blockName);
    void SectionUpdateFailed(string blockName, string reason);
}
