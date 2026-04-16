using System.Collections.Generic;
using System.Linq;

namespace SectionGenerator.Core.Sections;

/// <summary>
/// 层构建器接口 - 定义如何构建一个层
/// </summary>
public interface ILayerBuilder
{
    /// <summary>
    /// 层名称
    /// </summary>
    string LayerName { get; }
    
    /// <summary>
    /// 构建层位置
    /// </summary>
    /// <param name="context">构建上下文</param>
    /// <returns>层位置信息</returns>
    LayerPosition Build(SectionBuildContext context);
}

/// <summary>
/// 层构建上下文 - 传递构建所需的数据和状态
/// </summary>
public sealed class SectionBuildContext
{
    /// <summary>
    /// 剖面设置
    /// </summary>
    public SectionSettings Settings { get; }
    
    /// <summary>
    /// 剖面长度
    /// </summary>
    public double Length { get; }
    
    /// <summary>
    /// 已构建的层列表
    /// </summary>
    public IReadOnlyList<LayerPosition> BuiltLayers => _builtLayers.AsReadOnly();
    
    private readonly List<LayerPosition> _builtLayers = new();
    
    public SectionBuildContext(SectionSettings settings, double length)
    {
        Settings = settings;
        Length = length;
    }
    
    /// <summary>
    /// 添加已构建的层
    /// </summary>
    internal void AddLayer(LayerPosition layer)
    {
        _builtLayers.Add(layer);
    }
    
    /// <summary>
    /// 获取指定名称的层
    /// </summary>
    public LayerPosition? GetLayer(string name)
    {
        return _builtLayers.LastOrDefault(l => l.Name == name);
    }
    
    /// <summary>
    /// 获取最后一层
    /// </summary>
    public LayerPosition? GetLastLayer()
    {
        return _builtLayers.LastOrDefault();
    }
    
    /// <summary>
    /// 计算坡度偏移量
    /// </summary>
    public double CalculateSlopeDelta()
    {
        return Settings.HasSlope ? Length * Settings.SlopeValue : 0.0;
    }
}

/// <summary>
/// 层位置类型
/// </summary>
public enum LayerPositionType
{
    /// <summary>
    /// 从底部开始（如底板）
    /// </summary>
    Bottom,
    
    /// <summary>
    /// 从顶部开始（如吊顶）
    /// </summary>
    Top,
    
    /// <summary>
    /// 跟随上一层
    /// </summary>
    Follow
}
