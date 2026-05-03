namespace MetroToolKits.SectionGenerator.App.Models;

public sealed class ConvertedElementMetadataIssue
{
    public string Handle { get; init; } = string.Empty;

    public string LayerName { get; init; } = string.Empty;

    public string? CurrentConvertedType { get; init; }

    public string? CurrentTemplateId { get; init; }

    public ConvertedElementMetadataIssueReason Reason { get; init; }

    public string? RecommendedTemplateId { get; init; }

    public string? RecommendedTemplateName { get; init; }
}
