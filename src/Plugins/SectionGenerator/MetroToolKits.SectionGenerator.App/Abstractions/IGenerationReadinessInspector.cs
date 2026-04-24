namespace MetroToolKits.SectionGenerator.App.Abstractions;

/// <summary>
/// 检查当前图纸中的构件识别就绪度。
/// </summary>
public interface IGenerationReadinessInspector
{
    GenerationReadinessSummary Inspect();
}

public sealed class GenerationReadinessSummary
{
    public int TotalRecognizableElementCount { get; init; }

    public int RecognizableWallCount { get; init; }

    public int RecognizableColumnCount { get; init; }

    public int RecognizableSlabCount { get; init; }

    public int ConvertedEntityCount { get; init; }

    public int TemplatedWallCount { get; init; }

    public int TemplatedSlabCount { get; init; }

    public int LegacyWallCount { get; init; }

    public int LegacySlabCount { get; init; }

    public bool HasRecognizableElements => TotalRecognizableElementCount > 0;
}
