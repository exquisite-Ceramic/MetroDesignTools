using MetroToolKits.SectionGenerator.Contracts.Output;

namespace MetroToolKits.SectionGenerator.App.ViewModels;

public sealed class SectionOutputConfigViewModel
{
    public bool GenerateAnnotations { get; set; }

    public bool HatchEnabled { get; set; }

    public HatchStyleViewModel WallHatch { get; } = new();

    public HatchStyleViewModel ColumnHatch { get; } = new();

    public HatchStyleViewModel SlabHatch { get; } = new();

    public string CutLineLayer { get; set; } = string.Empty;

    public string SightLineLayer { get; set; } = string.Empty;

    public string AnnotationLayer { get; set; } = string.Empty;

    public string WallHatchLayer { get; set; } = string.Empty;

    public string ColumnHatchLayer { get; set; } = string.Empty;

    public string SlabHatchLayer { get; set; } = string.Empty;

    public string StructuralLayer { get; set; } = string.Empty;

    public string FinishLayer { get; set; } = string.Empty;

    public void Load(SectionOutputConfigDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);

        GenerateAnnotations = dto.AnnotationOptions?.GenerateAnnotations == true;
        HatchEnabled = dto.HatchOptions?.Enabled == true;
        WallHatch.Load(dto.HatchOptions?.WallHatch);
        ColumnHatch.Load(dto.HatchOptions?.ColumnHatch);
        SlabHatch.Load(dto.HatchOptions?.SlabHatch);

        CutLineLayer = dto.LayerOptions?.CutLineLayer ?? string.Empty;
        SightLineLayer = dto.LayerOptions?.SightLineLayer ?? string.Empty;
        AnnotationLayer = dto.LayerOptions?.AnnotationLayer ?? string.Empty;
        WallHatchLayer = dto.LayerOptions?.WallHatchLayer ?? string.Empty;
        ColumnHatchLayer = dto.LayerOptions?.ColumnHatchLayer ?? string.Empty;
        SlabHatchLayer = dto.LayerOptions?.SlabHatchLayer ?? string.Empty;
        StructuralLayer = dto.LayerOptions?.StructuralLayer ?? string.Empty;
        FinishLayer = dto.LayerOptions?.FinishLayer ?? string.Empty;
    }

    public SectionOutputConfigDto ToDto()
        => new()
        {
            AnnotationOptions = new AnnotationOptionsDto
            {
                GenerateAnnotations = GenerateAnnotations
            },
            HatchOptions = new HatchOptionsDto
            {
                Enabled = HatchEnabled,
                WallHatch = WallHatch.ToDto(),
                ColumnHatch = ColumnHatch.ToDto(),
                SlabHatch = SlabHatch.ToDto()
            },
            LayerOptions = new LayerOptionsDto
            {
                CutLineLayer = CutLineLayer.Trim(),
                SightLineLayer = SightLineLayer.Trim(),
                AnnotationLayer = AnnotationLayer.Trim(),
                WallHatchLayer = WallHatchLayer.Trim(),
                ColumnHatchLayer = ColumnHatchLayer.Trim(),
                SlabHatchLayer = SlabHatchLayer.Trim(),
                StructuralLayer = StructuralLayer.Trim(),
                FinishLayer = FinishLayer.Trim()
            }
        };
}

public sealed class HatchStyleViewModel
{
    public string PatternName { get; set; } = "ANSI31";

    public double Scale { get; set; } = 100.0;

    public double Angle { get; set; }

    public bool UseByLayer { get; set; } = true;

    public void Load(HatchStyleDto? dto)
    {
        var resolved = dto ?? new HatchStyleDto();
        PatternName = resolved.PatternName;
        Scale = resolved.Scale;
        Angle = resolved.Angle;
        UseByLayer = resolved.UseByLayer;
    }

    public HatchStyleDto ToDto()
        => new()
        {
            PatternName = PatternName.Trim(),
            Scale = Scale,
            Angle = Angle,
            UseByLayer = UseByLayer
        };
}
