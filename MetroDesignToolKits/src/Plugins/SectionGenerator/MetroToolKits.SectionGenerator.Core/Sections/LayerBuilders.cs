using System;

namespace SectionGenerator.Core.Sections;

/// <summary>
/// 底板构建器
/// </summary>
public sealed class BottomSlabBuilder : ILayerBuilder
{
    public string LayerName => "底板";
    
    public LayerPosition Build(SectionBuildContext context)
    {
        var thickness = context.Settings.BottomSlabThickness;
        
        return new LayerPosition(
            LayerName,
            -thickness,     // 起点底部
            -thickness,     // 终点底部
            0.0,            // 起点顶部（地面层）
            0.0,            // 终点顶部
            "AR-CONC"       // 混凝土填充
        );
    }
}

/// <summary>
/// 顶板构建器
/// </summary>
public sealed class TopSlabBuilder : ILayerBuilder
{
    public string LayerName => "顶板";
    
    public LayerPosition Build(SectionBuildContext context)
    {
        var settings = context.Settings;
        var slopeDelta = context.CalculateSlopeDelta();
        
        var bottomStart = settings.FloorHeight;
        var bottomEnd = settings.FloorHeight + slopeDelta;
        
        return new LayerPosition(
            LayerName,
            bottomStart,
            bottomEnd,
            bottomStart + settings.TopSlabThickness,
            bottomEnd + settings.TopSlabThickness,
            "AR-CONC"
        );
    }
}

/// <summary>
/// 装修面层构建器
/// </summary>
public sealed class FinishLayerBuilder : ILayerBuilder
{
    public string LayerName => "装修面层";
    
    public LayerPosition Build(SectionBuildContext context)
    {
        // 依赖顶板的位置
        var topSlab = context.GetLayer("顶板");
        if (topSlab == null)
        {
            throw new InvalidOperationException("装修面层必须在顶板之后构建");
        }
        
        var thickness = context.Settings.FinishThickness;
        
        return new LayerPosition(
            LayerName,
            topSlab.TopStart,
            topSlab.TopEnd,
            topSlab.TopStart + thickness,
            topSlab.TopEnd + thickness,
            "ANSI31"        // 斜线填充
        );
    }
}

/// <summary>
/// 吊顶构建器 - 示例：如何新增层类型
/// </summary>
public sealed class CeilingBuilder : ILayerBuilder
{
    public string LayerName => "吊顶";
    
    /// <summary>
    /// 吊顶距顶板的距离，可在配置中设置
    /// </summary>
    public double OffsetFromTopSlab { get; set; } = 300.0;
    
    public LayerPosition Build(SectionBuildContext context)
    {
        // 依赖顶板的位置
        var topSlab = context.GetLayer("顶板");
        if (topSlab == null)
        {
            throw new InvalidOperationException("吊顶必须在顶板之后构建");
        }
        
        // 吊顶厚度，可从配置读取
        var thickness = 100.0;
        
        var bottomStart = topSlab.TopStart + OffsetFromTopSlab;
        var bottomEnd = topSlab.TopEnd + OffsetFromTopSlab;
        
        return new LayerPosition(
            LayerName,
            bottomStart,
            bottomEnd,
            bottomStart + thickness,
            bottomEnd + thickness,
            "AR-SAND"       // 沙点填充
        );
    }
}

/// <summary>
/// 保温层构建器 - 示例：另一个可扩展的层类型
/// </summary>
public sealed class InsulationLayerBuilder : ILayerBuilder
{
    public string LayerName => "保温层";
    
    /// <summary>
    /// 保温层厚度
    /// </summary>
    public double Thickness { get; set; } = 50.0;
    
    /// <summary>
    /// 依附的层名称
    /// </summary>
    public string AttachToLayer { get; set; } = "顶板";
    
    /// <summary>
    /// 在依附层的上方还是下方
    /// </summary>
    public bool AboveAttachLayer { get; set; } = true;
    
    public LayerPosition Build(SectionBuildContext context)
    {
        var baseLayer = context.GetLayer(AttachToLayer);
        if (baseLayer == null)
        {
            throw new InvalidOperationException($"{LayerName}必须在{AttachToLayer}之后构建");
        }
        
        double bottomStart, bottomEnd;
        
        if (AboveAttachLayer)
        {
            // 在依附层上方
            bottomStart = baseLayer.TopStart;
            bottomEnd = baseLayer.TopEnd;
        }
        else
        {
            // 在依附层下方
            bottomStart = baseLayer.BottomStart - Thickness;
            bottomEnd = baseLayer.BottomEnd - Thickness;
        }
        
        return new LayerPosition(
            LayerName,
            bottomStart,
            bottomEnd,
            bottomStart + Thickness,
            bottomEnd + Thickness,
            "INSUL"         // 保温材料填充
        );
    }
}
