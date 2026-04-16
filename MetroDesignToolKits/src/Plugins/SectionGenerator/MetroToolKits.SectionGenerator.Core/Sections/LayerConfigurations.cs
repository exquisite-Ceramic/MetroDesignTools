using System.Collections.Generic;

namespace SectionGenerator.Core.Sections;

/// <summary>
/// 预定义的层配置
/// </summary>
public static class LayerConfigurations
{
    /// <summary>
    /// 标准配置：底板 + 顶板 + 装修面层
    /// </summary>
    public static IEnumerable<ILayerBuilder> Standard => new ILayerBuilder[]
    {
        new BottomSlabBuilder(),
        new TopSlabBuilder(),
        new FinishLayerBuilder()
    };

    /// <summary>
    /// 带吊顶配置：底板 + 顶板 + 装修面层 + 吊顶
    /// </summary>
    public static IEnumerable<ILayerBuilder> WithCeiling => new ILayerBuilder[]
    {
        new BottomSlabBuilder(),
        new TopSlabBuilder(),
        new FinishLayerBuilder(),
        new CeilingBuilder()
    };

    /// <summary>
    /// 带保温层配置：底板 + 顶板 + 保温层 + 装修面层
    /// </summary>
    public static IEnumerable<ILayerBuilder> WithInsulation => new ILayerBuilder[]
    {
        new BottomSlabBuilder(),
        new TopSlabBuilder(),
        new InsulationLayerBuilder { Thickness = 50.0, AttachToLayer = "顶板", AboveAttachLayer = true },
        new FinishLayerBuilder()
    };

    /// <summary>
    /// 完整配置：底板 + 顶板 + 保温层 + 装修面层 + 吊顶
    /// </summary>
    public static IEnumerable<ILayerBuilder> Full => new ILayerBuilder[]
    {
        new BottomSlabBuilder(),
        new TopSlabBuilder(),
        new InsulationLayerBuilder(),
        new FinishLayerBuilder(),
        new CeilingBuilder()
    };

    /// <summary>
    /// 带看线配置：底板 + 顶板 + 装修面层 + 看线
    /// </summary>
    public static IEnumerable<ILayerBuilder> WithSightLine => new ILayerBuilder[]
    {
        new BottomSlabBuilder(),
        new TopSlabBuilder(),
        new FinishLayerBuilder(),
        new SightLineLayerBuilder { Height = 1100, EyeHeight = 1600 }
    };

    /// <summary>
    /// 带墙体和看线配置：底板 + 顶板 + 墙 + 装修面层 + 看线
    /// 用于测试看线遮挡
    /// </summary>
    public static IEnumerable<ILayerBuilder> WithWallAndSightLine => new ILayerBuilder[]
    {
        new BottomSlabBuilder(),
        new TopSlabBuilder(),
        new WallLayerBuilder { Position = 2000, Thickness = 200, Height = 2500 },
        new FinishLayerBuilder(),
        new SightLineLayerBuilder { Height = 1100, EyeHeight = 1600 }
    };

    /// <summary>
    /// 自定义配置
    /// </summary>
    public static IEnumerable<ILayerBuilder> Custom(params ILayerBuilder[] builders) => builders;

    /// <summary>
    /// 从配置名称获取层配置
    /// </summary>
    public static IEnumerable<ILayerBuilder> GetConfiguration(string name) => name.ToLower() switch
    {
        "standard" or "标准" => Standard,
        "withceiling" or "带吊顶" => WithCeiling,
        "withinsulation" or "带保温" => WithInsulation,
        "full" or "完整" => Full,
        "withsightline" or "带看线" => WithSightLine,
        "withwallandsightline" or "带墙和看线" => WithWallAndSightLine,
        _ => Standard
    };
}
