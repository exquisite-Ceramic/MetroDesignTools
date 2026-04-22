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

    /// <summary>扫描图纸中所有剖面块，返回块句柄列表</summary>
    IReadOnlyList<string> FindAllSectionBlockHandles();

    /// <summary>读取块定义中可见剖面几何的最小 X，用于旧快照兼容。</summary>
    double? ResolveBlockGeometryAnchorX(string blockHandle);
}
