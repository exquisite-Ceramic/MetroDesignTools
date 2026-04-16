namespace MetroToolKits.SectionGenerator.App.UseCases;

public interface IUpdateSectionUseCase
{
    UpdateSectionResult Execute(UpdateSectionRequest request);
}

public sealed class UpdateSectionRequest
{
    /// <summary>要更新的剖面块句柄</summary>
    public string BlockHandle { get; set; } = string.Empty;
}

public sealed class UpdateSectionResult
{
    public bool    Success      { get; set; }
    public string? NewBlockName { get; set; }
    public string? ErrorMessage { get; set; }
}
