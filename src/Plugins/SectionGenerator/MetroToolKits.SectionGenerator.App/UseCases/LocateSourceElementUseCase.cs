using Microsoft.Extensions.Logging;
using MetroToolKits.SectionGenerator.App.Abstractions;

namespace MetroToolKits.SectionGenerator.App.UseCases;

/// <summary>
/// 剖面实体定位源构件用例
/// </summary>
public sealed class LocateSourceElementUseCase : ILocateSourceElementUseCase
{
    private readonly ISectionReferenceRepository _referenceRepository;
    private readonly ILogger<LocateSourceElementUseCase> _logger;

    public LocateSourceElementUseCase(
        ISectionReferenceRepository referenceRepository,
        ILogger<LocateSourceElementUseCase> logger)
    {
        _referenceRepository = referenceRepository;
        _logger = logger;
    }

    public LocateSourceElementResult Execute(LocateSourceElementRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.SectionEntityHandle))
        {
            return new LocateSourceElementResult
            {
                Success = false,
                ErrorMessage = "未提供剖面实体句柄"
            };
        }

        var reference = _referenceRepository.GetSectionEntityReference(request.SectionEntityHandle);
        if (reference == null || string.IsNullOrWhiteSpace(reference.SourceHandle))
        {
            _logger.LogInformation("剖面实体 {Handle} 未找到源构件引用", request.SectionEntityHandle);
            return new LocateSourceElementResult
            {
                Success = false,
                ErrorMessage = "所选剖面实体没有源构件引用"
            };
        }

        _logger.LogInformation("剖面实体 {SectionHandle} 对应源构件 {SourceHandle}",
            request.SectionEntityHandle, reference.SourceHandle);

        return new LocateSourceElementResult
        {
            Success = true,
            SourceHandle = reference.SourceHandle,
            ElementType = reference.ElementType
        };
    }
}
