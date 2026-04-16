namespace SectionGenerator.Core.Sections;

/// <summary>
/// 剖面生成器接口
/// </summary>
public interface ISectionGenerator
{
    /// <summary>
    /// 生成剖面
    /// </summary>
    /// <param name="def">剖面定义</param>
    /// <param name="settings">剖面设置</param>
    /// <returns>剖面生成结果</returns>
    SectionResult Generate(SectionDefinition def, SectionSettings settings);
}
