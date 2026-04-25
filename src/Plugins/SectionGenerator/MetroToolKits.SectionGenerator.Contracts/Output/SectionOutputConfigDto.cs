namespace MetroToolKits.SectionGenerator.Contracts.Output;

public sealed class SectionOutputConfigDto
{
    public AnnotationOptionsDto? AnnotationOptions { get; init; } = new();

    public HatchOptionsDto? HatchOptions { get; init; } = new();

    public LayerOptionsDto? LayerOptions { get; init; } = new();
}

public sealed class AnnotationOptionsDto
{
    public bool GenerateAnnotations { get; init; }
}

public sealed class HatchOptionsDto
{
    public bool Enabled { get; init; }

    public HatchStyleDto? WallHatch { get; init; } = new();

    public HatchStyleDto? ColumnHatch { get; init; } = new();

    public HatchStyleDto? SlabHatch { get; init; } = new();
}

public sealed class HatchStyleDto
{
    public string PatternName { get; init; } = "ANSI31";

    public double Scale { get; init; } = 100.0;

    public double Angle { get; init; }

    public bool UseByLayer { get; init; } = true;
}

public sealed class LayerOptionsDto
{
    public string CutLineLayer { get; init; } = "MK_剖切线";

    public string SightLineLayer { get; init; } = "MK_看线";

    public string AnnotationLayer { get; init; } = "MK_标注";

    public string WallHatchLayer { get; init; } = "MK_墙填充";

    public string ColumnHatchLayer { get; init; } = "MK_柱填充";

    public string SlabHatchLayer { get; init; } = "MK_楼板填充";

    public string StructuralLayer { get; init; } = "MK_结构输出";

    public string FinishLayer { get; init; } = "MK_装修输出";
}
