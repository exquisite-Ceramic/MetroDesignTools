using MetroToolKits.SectionGenerator.App.Abstractions;
using MetroToolKits.SectionGenerator.App.Models;
using MetroToolKits.SectionGenerator.Contracts.Output;

namespace MetroToolKits.SectionGenerator.App.Support;

public sealed class SectionOutputConfigMapper : ISectionOutputConfigMapper
{
    public SectionOutputConfigDto ToDto(SectionOutputConfig? outputConfig)
    {
        var resolvedOutput = outputConfig ?? new SectionOutputConfig();
        var annotation = resolvedOutput.AnnotationOptions ?? new AnnotationOptions();
        var hatch = resolvedOutput.HatchOptions ?? new HatchOptions();
        var layers = resolvedOutput.LayerOptions ?? new LayerOptions();
        var defaultLayers = new LayerOptions();

        return new SectionOutputConfigDto
        {
            AnnotationOptions = new AnnotationOptionsDto
            {
                GenerateAnnotations = annotation.GenerateAnnotations
            },
            HatchOptions = new HatchOptionsDto
            {
                Enabled = hatch.Enabled,
                WallHatch = MapHatchStyleToDto(hatch.WallHatch),
                ColumnHatch = MapHatchStyleToDto(hatch.ColumnHatch),
                SlabHatch = MapHatchStyleToDto(hatch.SlabHatch)
            },
            LayerOptions = new LayerOptionsDto
            {
                CutLineLayer = layers.CutLineLayer ?? defaultLayers.CutLineLayer,
                SightLineLayer = layers.SightLineLayer ?? defaultLayers.SightLineLayer,
                AnnotationLayer = layers.AnnotationLayer ?? defaultLayers.AnnotationLayer,
                WallHatchLayer = layers.WallHatchLayer ?? defaultLayers.WallHatchLayer,
                ColumnHatchLayer = layers.ColumnHatchLayer ?? defaultLayers.ColumnHatchLayer,
                SlabHatchLayer = layers.SlabHatchLayer ?? defaultLayers.SlabHatchLayer,
                StructuralLayer = layers.StructuralLayer ?? defaultLayers.StructuralLayer,
                FinishLayer = layers.FinishLayer ?? defaultLayers.FinishLayer
            }
        };
    }

    public SectionOutputConfig ToDomain(SectionOutputConfigDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var annotation = dto.AnnotationOptions ?? new AnnotationOptionsDto();
        var hatch = dto.HatchOptions ?? new HatchOptionsDto();
        var layers = dto.LayerOptions ?? new LayerOptionsDto();
        var defaultLayers = new LayerOptionsDto();

        return new SectionOutputConfig
        {
            AnnotationOptions = new AnnotationOptions
            {
                GenerateAnnotations = annotation.GenerateAnnotations
            },
            HatchOptions = new HatchOptions
            {
                Enabled = hatch.Enabled,
                WallHatch = MapHatchStyleToDomain(hatch.WallHatch),
                ColumnHatch = MapHatchStyleToDomain(hatch.ColumnHatch),
                SlabHatch = MapHatchStyleToDomain(hatch.SlabHatch)
            },
            LayerOptions = new LayerOptions
            {
                CutLineLayer = NormalizeString(layers.CutLineLayer, defaultLayers.CutLineLayer),
                SightLineLayer = NormalizeString(layers.SightLineLayer, defaultLayers.SightLineLayer),
                AnnotationLayer = NormalizeString(layers.AnnotationLayer, defaultLayers.AnnotationLayer),
                WallHatchLayer = NormalizeString(layers.WallHatchLayer, defaultLayers.WallHatchLayer),
                ColumnHatchLayer = NormalizeString(layers.ColumnHatchLayer, defaultLayers.ColumnHatchLayer),
                SlabHatchLayer = NormalizeString(layers.SlabHatchLayer, defaultLayers.SlabHatchLayer),
                StructuralLayer = NormalizeString(layers.StructuralLayer, defaultLayers.StructuralLayer),
                FinishLayer = NormalizeString(layers.FinishLayer, defaultLayers.FinishLayer)
            }
        };
    }

    public void ApplyToDocument(SectionOutputConfigDto dto, LoadedSectionConfig document)
    {
        ArgumentNullException.ThrowIfNull(dto);
        ArgumentNullException.ThrowIfNull(document);

        document.OutputConfig = ToDomain(dto);
    }

    private static HatchStyleDto MapHatchStyleToDto(HatchStyleOptions? style)
    {
        var resolvedStyle = style ?? HatchStyleOptions.CreateDefault();
        var defaultStyle = new HatchStyleDto();
        return new HatchStyleDto
        {
            PatternName = resolvedStyle.PatternName ?? defaultStyle.PatternName,
            Scale = resolvedStyle.Scale,
            Angle = resolvedStyle.Angle,
            UseByLayer = resolvedStyle.UseByLayer
        };
    }

    private static HatchStyleOptions MapHatchStyleToDomain(HatchStyleDto? style)
    {
        var resolvedStyle = style ?? new HatchStyleDto();
        var defaultStyle = new HatchStyleDto();
        return new HatchStyleOptions
        {
            PatternName = NormalizeString(resolvedStyle.PatternName, defaultStyle.PatternName),
            Scale = resolvedStyle.Scale,
            Angle = resolvedStyle.Angle,
            UseByLayer = resolvedStyle.UseByLayer
        };
    }

    private static string NormalizeString(string? value, string fallback)
        => value == null ? fallback : value.Trim();
}
