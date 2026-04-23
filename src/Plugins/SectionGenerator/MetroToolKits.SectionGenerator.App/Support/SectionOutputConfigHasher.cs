using System.Security.Cryptography;
using System.Text;
using MetroToolKits.SectionGenerator.App.Models;

namespace MetroToolKits.SectionGenerator.App.Support;

/// <summary>
/// 将图纸级输出配置折叠进楼层几何哈希，避免 Core 直接依赖宿主绘图配置。
/// </summary>
public static class SectionOutputConfigHasher
{
    public static string Combine(string geometryHash, SectionOutputConfig? outputConfig)
    {
        outputConfig ??= new SectionOutputConfig();
        outputConfig.AnnotationOptions ??= new AnnotationOptions();
        outputConfig.HatchOptions ??= new HatchOptions();
        outputConfig.HatchOptions.WallHatch ??= HatchStyleOptions.CreateDefault();
        outputConfig.HatchOptions.ColumnHatch ??= HatchStyleOptions.CreateDefault();
        outputConfig.HatchOptions.SlabHatch ??= HatchStyleOptions.CreateDefault();
        outputConfig.LayerOptions ??= new LayerOptions();

        var sb = new StringBuilder();
        sb.Append(geometryHash);
        sb.Append("|output:");
        sb.Append(outputConfig.AnnotationOptions.GenerateAnnotations);
        sb.Append('|');
        sb.Append(outputConfig.HatchOptions.Enabled);
        AppendHatchStyle(sb, outputConfig.HatchOptions.WallHatch);
        AppendHatchStyle(sb, outputConfig.HatchOptions.ColumnHatch);
        AppendHatchStyle(sb, outputConfig.HatchOptions.SlabHatch);
        sb.Append('|');
        sb.Append(outputConfig.LayerOptions.CutLineLayer);
        sb.Append('|');
        sb.Append(outputConfig.LayerOptions.SightLineLayer);
        sb.Append('|');
        sb.Append(outputConfig.LayerOptions.AnnotationLayer);
        sb.Append('|');
        sb.Append(outputConfig.LayerOptions.WallHatchLayer);
        sb.Append('|');
        sb.Append(outputConfig.LayerOptions.ColumnHatchLayer);
        sb.Append('|');
        sb.Append(outputConfig.LayerOptions.SlabHatchLayer);
        sb.Append('|');
        sb.Append(outputConfig.LayerOptions.StructuralLayer);
        sb.Append('|');
        sb.Append(outputConfig.LayerOptions.FinishLayer);

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString()));
        return Convert.ToHexString(bytes).ToLowerInvariant()[..16];
    }

    private static void AppendHatchStyle(StringBuilder sb, HatchStyleOptions style)
    {
        sb.Append('|');
        sb.Append(style.PatternName);
        sb.Append('|');
        sb.Append(style.Scale.ToString("F4"));
        sb.Append('|');
        sb.Append(style.Angle.ToString("F4"));
        sb.Append('|');
        sb.Append(style.UseByLayer);
    }
}
