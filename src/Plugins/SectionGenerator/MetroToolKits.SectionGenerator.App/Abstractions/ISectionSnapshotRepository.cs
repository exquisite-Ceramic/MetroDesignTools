using MetroToolKits.SectionGenerator.Core.Sections;

namespace MetroToolKits.SectionGenerator.App.Abstractions;

/// <summary>
/// 剖面快照仓储接口 - 读写块 XData 中的快照数据
/// </summary>
public interface ISectionSnapshotRepository
{
    /// <summary>将快照写入块的 XData</summary>
    void Save(string blockHandle, SectionSnapshot snapshot);

    /// <summary>从块的 XData 读取快照</summary>
    SectionSnapshot? Load(string blockHandle);
}

/// <summary>
/// 查询图纸中的剖面块。
/// </summary>
public interface ISectionBlockQueryService
{
    IReadOnlyList<string> FindAllSectionBlockHandles();
}

/// <summary>
/// 读取旧剖面块中与布局恢复相关的几何信息。
/// </summary>
public interface ISectionGeometryRecoveryService
{
    double? ResolveBlockGeometryAnchorX(string blockHandle);
}
