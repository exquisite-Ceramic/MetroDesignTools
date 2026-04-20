namespace MetroToolKits.SectionGenerator.App.UseCases;

/// <summary>
/// 平面构件查询关联剖面用例接口
/// </summary>
public interface IFindRelatedSectionsUseCase
{
    FindRelatedSectionsResult Execute(FindRelatedSectionsRequest request);
}

public sealed class FindRelatedSectionsRequest
{
    public string SourceHandle { get; set; } = string.Empty;
}

public sealed class FindRelatedSectionsResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public IReadOnlyList<RelatedSectionInfo> Sections { get; set; } = Array.Empty<RelatedSectionInfo>();
}

public sealed class RelatedSectionInfo
{
    public string BlockHandle { get; init; } = string.Empty;
    public string BlockName { get; init; } = string.Empty;
    public DateTime GeneratedAt { get; init; }
    public IReadOnlyList<string> RelatedFloors { get; init; } = Array.Empty<string>();
}
