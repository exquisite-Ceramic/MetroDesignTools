using System.Collections.Generic;
using System.Linq;
using SectionGenerator.App.Abstractions;
using SectionGenerator.Core.Geometry;
using SectionGenerator.Core.Sections;

namespace SectionGenerator.App.UseCases;

/// <summary>
/// 剖面生成用例编排器 - 应用层
/// </summary>
public sealed class SectionGenerationOrchestrator : CadServiceBase
{
    private readonly ISectionGenerator _sectionGenerator;
    private readonly ISectionDrawingService _drawingService;

    public SectionGenerationOrchestrator(
        ICadSession session, 
        ITransactionRunner tr,
        ISectionGenerator sectionGenerator,
        ISectionDrawingService drawingService)
        : base(session, tr)
    {
        _sectionGenerator = sectionGenerator;
        _drawingService = drawingService;
    }

    /// <summary>
    /// 生成并绘制剖面图
    /// </summary>
    public void GenerateAndDrawSection(
        Vec2 origin,
        Vec2 direction,
        double length,
        IReadOnlyList<double> intersectionParams,
        SectionSettings settings)
    {
        // 1. 构建剖面定义
        var definition = new SectionDefinition(origin, direction, length);

        // 2. 调用领域层生成剖面数据
        var result = _sectionGenerator.Generate(definition, settings);

        // 3. 调用基础设施层绘制图形
        _drawingService.DrawSection(result, intersectionParams);
    }
}
