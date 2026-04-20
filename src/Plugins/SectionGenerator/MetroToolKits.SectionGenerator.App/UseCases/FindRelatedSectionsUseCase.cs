using Microsoft.Extensions.Logging;
using MetroToolKits.SectionGenerator.App.Abstractions;

namespace MetroToolKits.SectionGenerator.App.UseCases;

/// <summary>
/// 平面构件查询关联剖面用例
/// </summary>
public sealed class FindRelatedSectionsUseCase : IFindRelatedSectionsUseCase
{
    private readonly ISectionSnapshotRepository _snapshotRepository;
    private readonly ILogger<FindRelatedSectionsUseCase> _logger;

    public FindRelatedSectionsUseCase(
        ISectionSnapshotRepository snapshotRepository,
        ILogger<FindRelatedSectionsUseCase> logger)
    {
        _snapshotRepository = snapshotRepository;
        _logger = logger;
    }

    public FindRelatedSectionsResult Execute(FindRelatedSectionsRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.SourceHandle))
        {
            return new FindRelatedSectionsResult
            {
                Success = false,
                ErrorMessage = "未提供源构件句柄"
            };
        }

        var results = new List<RelatedSectionInfo>();
        foreach (var blockHandle in _snapshotRepository.FindAllSectionBlockHandles())
        {
            var snapshot = _snapshotRepository.Load(blockHandle);
            if (snapshot == null)
            {
                continue;
            }

            var relatedFloors = snapshot.FloorSnapshots
                .Where(f => f.SourceElementHandles?.Contains(request.SourceHandle, StringComparer.OrdinalIgnoreCase) == true)
                .Select(f => f.FloorName)
                .ToList();

            if (relatedFloors.Count == 0)
            {
                continue;
            }

            results.Add(new RelatedSectionInfo
            {
                BlockHandle = blockHandle,
                BlockName = snapshot.BlockName,
                GeneratedAt = snapshot.GeneratedAt,
                RelatedFloors = relatedFloors
            });
        }

        _logger.LogInformation("源构件 {Handle} 查询到 {Count} 个关联剖面", request.SourceHandle, results.Count);

        return new FindRelatedSectionsResult
        {
            Success = true,
            Sections = results
                .OrderByDescending(r => r.GeneratedAt)
                .ThenBy(r => r.BlockName, StringComparer.OrdinalIgnoreCase)
                .ToList()
        };
    }
}
