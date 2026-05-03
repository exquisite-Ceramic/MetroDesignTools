using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MetroToolKits.Foundation.Building.Elements;
using MetroToolKits.Foundation.Building.Types;
using MetroToolKits.Foundation.Core.Diagnostics;
using MetroToolKits.Foundation.Core.Geometry;
using MetroToolKits.SectionGenerator.Core.Sections.Hatching;

namespace MetroToolKits.SectionGenerator.Core.Sections;

/// <summary>
/// 楼层竖向轮廓构建器。
/// 模板优先，未绑定模板时回退到主线 legacy 厚度字段。
/// </summary>
public sealed class FloorVerticalProfileBuilder
{
    public const string BoundarySlabTemplateMissingCode = "SectionGenerator.FloorConfigLoad.BoundarySlabTemplateMissing";

    private readonly ISlabAssemblyTemplateCatalog _slabTemplateCatalog;
    private readonly ISlabAssemblyBuilder _slabAssemblyBuilder;
    private readonly ILogger<FloorVerticalProfileBuilder> _logger;

    /// <summary>
    /// 仅用于测试和兼容回退；生产链请通过 DI 注入共享实例。
    /// </summary>
    public FloorVerticalProfileBuilder()
        : this(
            new InMemorySlabAssemblyTemplateCatalog(),
            new SlabAssemblyBuilder(),
            NullLogger<FloorVerticalProfileBuilder>.Instance)
    {
    }

    public FloorVerticalProfileBuilder(
        ISlabAssemblyTemplateCatalog slabTemplateCatalog,
        ISlabAssemblyBuilder slabAssemblyBuilder)
        : this(
            slabTemplateCatalog,
            slabAssemblyBuilder,
            NullLogger<FloorVerticalProfileBuilder>.Instance)
    {
    }

    public FloorVerticalProfileBuilder(
        ISlabAssemblyTemplateCatalog slabTemplateCatalog,
        ISlabAssemblyBuilder slabAssemblyBuilder,
        ILogger<FloorVerticalProfileBuilder> logger)
    {
        _slabTemplateCatalog = slabTemplateCatalog;
        _slabAssemblyBuilder = slabAssemblyBuilder;
        _logger = logger;
    }

    public FloorVerticalProfile Build(
        FloorConfig floor,
        double sectionLength,
        double baseElevation)
    {
        var bottomDefinition = ResolveBoundaryDefinition(
            floor,
            "底边界板",
            floor.BottomBoundarySlab,
            createLegacyTemplate: () => CreateLegacyBottomTemplate(floor),
            logResolution: true);
        var topDefinition = ResolveBoundaryDefinition(
            floor,
            "顶边界板",
            floor.TopBoundarySlab,
            createLegacyTemplate: () => CreateLegacyTopTemplate(floor),
            logResolution: true);

        var bottomSlope = ResolveBoundarySlope(floor, bottomDefinition.Config, useLegacySlope: false);
        var topSlope = ResolveBoundarySlope(floor, topDefinition.Config, useLegacySlope: true);
        var bottomStructuralSlopeDelta = bottomSlope.TargetsFinishLayer
            ? 0d
            : sectionLength * bottomSlope.Value;
        var bottomFinishSlopeDelta = sectionLength * bottomSlope.Value;
        var topStructuralSlopeDelta = topSlope.TargetsFinishLayer
            ? 0d
            : sectionLength * topSlope.Value;
        var topFinishSlopeDelta = sectionLength * topSlope.Value;

        var bottomTopStart = baseElevation;
        var bottomTopEnd = baseElevation + bottomStructuralSlopeDelta;
        var bottomBottomStart = bottomTopStart + bottomDefinition.BottommostOffsetFromTop;
        var bottomBottomEnd = bottomTopStart + bottomFinishSlopeDelta + bottomDefinition.BottommostOffsetFromTop;
        var bottomStructuralTopStart = bottomTopStart + bottomDefinition.CoreTopOffsetFromTop;
        var bottomStructuralTopEnd = bottomTopEnd + bottomDefinition.CoreTopOffsetFromTop;
        var bottomStructuralBottomStart = bottomTopStart + bottomDefinition.CoreBottomOffsetFromTop;
        var bottomStructuralBottomEnd = bottomTopEnd + bottomDefinition.CoreBottomOffsetFromTop;

        var topBottomStart = baseElevation + floor.Height;
        var topBottomEnd = topBottomStart + topStructuralSlopeDelta;
        var topTopStart = topBottomStart + topDefinition.TopmostOffsetFromBottom;
        var topTopEnd = topBottomStart + topFinishSlopeDelta + topDefinition.TopmostOffsetFromBottom;
        var topStructuralBottomStart = topBottomStart + topDefinition.CoreBottomOffsetFromBottom;
        var topStructuralBottomEnd = topBottomEnd + topDefinition.CoreBottomOffsetFromBottom;
        var topStructuralTopStart = topBottomStart + topDefinition.CoreTopOffsetFromBottom;
        var topStructuralTopEnd = topBottomEnd + topDefinition.CoreTopOffsetFromBottom;

        return new FloorVerticalProfile
        {
            SectionLength = sectionLength,
            BottomStructuralBottom = new ProfileEdge(0, sectionLength, bottomStructuralBottomStart, bottomStructuralBottomEnd),
            BottomStructuralTop = new ProfileEdge(0, sectionLength, bottomStructuralTopStart, bottomStructuralTopEnd),
            BottomBoundaryBottom = new ProfileEdge(0, sectionLength, bottomBottomStart, bottomBottomEnd),
            BottomBoundaryTop = new ProfileEdge(0, sectionLength, bottomTopStart, bottomTopEnd),
            TopStructuralBottom = new ProfileEdge(0, sectionLength, topStructuralBottomStart, topStructuralBottomEnd),
            TopStructuralTop = new ProfileEdge(0, sectionLength, topStructuralTopStart, topStructuralTopEnd),
            TopBoundaryBottom = new ProfileEdge(0, sectionLength, topBottomStart, topBottomEnd),
            TopBoundaryTop = new ProfileEdge(0, sectionLength, topTopStart, topTopEnd)
        };
    }

    private static BoundarySlope ResolveBoundarySlope(
        FloorConfig floor,
        BoundarySlabConfig boundaryConfig,
        bool useLegacySlope)
    {
        if (boundaryConfig.SlopeEnabled)
        {
            return new BoundarySlope(
                boundaryConfig.SlopeValue,
                IsFinishLayerSlopeTarget(boundaryConfig.SlopeTarget));
        }

        if (useLegacySlope && floor.HasSlope)
        {
            return new BoundarySlope(
                floor.SlopeValue,
                IsFinishLayerSlopeTarget(floor.SlopeTarget));
        }

        return new BoundarySlope(0d, false);
    }

    private static bool IsFinishLayerSlopeTarget(string? target)
        => string.Equals(target, "FinishLayer", StringComparison.OrdinalIgnoreCase);

    public IReadOnlyList<Line3D> BuildBoundaryLines(
        FloorConfig floor,
        FloorVerticalProfile profile,
        IReadOnlyList<(double StartX, double EndX)> wallIntervals,
        FloorStackLayoutItem? stackLayoutItem = null)
        => BuildBoundaryLineSegments(floor, profile, wallIntervals, stackLayoutItem)
            .Select(segment => segment.Line)
            .ToList();

    public IReadOnlyList<SectionLineSegment> BuildBoundaryLineSegments(
        FloorConfig floor,
        FloorVerticalProfile profile,
        IReadOnlyList<(double StartX, double EndX)> wallIntervals,
        FloorStackLayoutItem? stackLayoutItem = null)
    {
        var layoutItem = stackLayoutItem ?? FloorStackLayoutItem.CreateDrawAll(floor);
        var segments = new List<SectionLineSegment>();
        var bottomDefinition = ResolveBoundaryDefinition(
            floor,
            "底边界板",
            floor.BottomBoundarySlab,
            () => CreateLegacyBottomTemplate(floor));
        var topDefinition = ResolveBoundaryDefinition(
            floor,
            "顶边界板",
            floor.TopBoundarySlab,
            () => CreateLegacyTopTemplate(floor));
        var bottomFinishSlopeDelta = ResolveBoundarySlope(floor, bottomDefinition.Config, useLegacySlope: false).TargetsFinishLayer
            ? profile.BottomBoundaryBottom.EndY - (profile.BottomBoundaryTop.EndY + bottomDefinition.BottommostOffsetFromTop)
            : 0d;
        var topFinishSlopeDelta = ResolveBoundarySlope(floor, topDefinition.Config, useLegacySlope: true).TargetsFinishLayer
            ? profile.TopBoundaryTop.EndY - (profile.TopBoundaryBottom.EndY + topDefinition.TopmostOffsetFromBottom)
            : 0d;

        if (layoutItem.BottomBoundary.DrawLines)
        {
            segments.AddRange(BuildBoundaryLinesFor(
                bottomDefinition,
                anchorAtTop: true,
                anchorStartY: profile.BottomBoundaryTop.StartY,
                anchorEndY: profile.BottomBoundaryTop.EndY,
                profile.SectionLength,
                wallIntervals,
                bottomFinishSlopeDelta));
        }

        if (layoutItem.TopBoundary.DrawLines)
        {
            segments.AddRange(BuildBoundaryLinesFor(
                topDefinition,
                anchorAtTop: false,
                anchorStartY: profile.TopBoundaryBottom.StartY,
                anchorEndY: profile.TopBoundaryBottom.EndY,
                profile.SectionLength,
                wallIntervals,
                topFinishSlopeDelta));
        }

        if (layoutItem.BottomBoundary.DrawLines && layoutItem.TopBoundary.DrawLines)
        {
            segments.Add(new SectionLineSegment
            {
                Line = new Line3D(
                    new Point3D(0, profile.BottomBoundaryBottom.StartY, 0),
                    new Point3D(0, profile.TopBoundaryTop.StartY, 0)),
                Role = SectionLineRole.Structural
            });
            segments.Add(new SectionLineSegment
            {
                Line = new Line3D(
                    new Point3D(profile.SectionLength, profile.BottomBoundaryBottom.EndY, 0),
                    new Point3D(profile.SectionLength, profile.TopBoundaryTop.EndY, 0)),
                Role = SectionLineRole.Structural
            });
        }

        return segments
            .Where(segment => segment.Line.Start.DistanceTo(segment.Line.End) > 1e-6)
            .ToList();
    }

    public IReadOnlyList<SectionHatchRegion> BuildBoundaryHatchRegions(
        FloorConfig floor,
        FloorVerticalProfile profile,
        FloorStackLayoutItem? stackLayoutItem = null)
    {
        var layoutItem = stackLayoutItem ?? FloorStackLayoutItem.CreateDrawAll(floor);
        var regions = new List<SectionHatchRegion>();
        if (layoutItem.BottomBoundary.DrawHatch)
        {
            TryAddStructuralBoundaryHatch(
                regions,
                profile.BottomStructuralBottom.StartY,
                profile.BottomStructuralBottom.EndY,
                profile.BottomStructuralTop.StartY,
                profile.BottomStructuralTop.EndY,
                profile.SectionLength);
        }

        if (layoutItem.TopBoundary.DrawHatch)
        {
            TryAddStructuralBoundaryHatch(
                regions,
                profile.TopStructuralBottom.StartY,
                profile.TopStructuralBottom.EndY,
                profile.TopStructuralTop.StartY,
                profile.TopStructuralTop.EndY,
                profile.SectionLength);
        }

        return regions;
    }

    private static void TryAddStructuralBoundaryHatch(
        ICollection<SectionHatchRegion> regions,
        double bottomStart,
        double bottomEnd,
        double topStart,
        double topEnd,
        double sectionLength)
    {
        if (Math.Abs(topStart - bottomStart) <= 1e-6 &&
            Math.Abs(topEnd - bottomEnd) <= 1e-6)
        {
            return;
        }

        if (SectionHatchRegionFactory.TryCreateQuad(
                SectionHatchCategory.Slab,
                new Point3D(0, bottomStart, 0),
                new Point3D(sectionLength, bottomEnd, 0),
                new Point3D(sectionLength, topEnd, 0),
                new Point3D(0, topStart, 0),
                out var region,
                out _))
        {
            regions.Add(region!);
        }
    }

    private IReadOnlyList<SectionLineSegment> BuildBoundaryLinesFor(
        BoundaryTemplateDefinition definition,
        bool anchorAtTop,
        double anchorStartY,
        double anchorEndY,
        double sectionLength,
        IReadOnlyList<(double StartX, double EndX)> wallIntervals,
        double finishSlopeDelta = 0d)
    {
        var lines = new List<SectionLineSegment>();
        var visibleCore = definition.Sections.FirstOrDefault(layer => layer.IsCore && layer.VisibleInSection);
        if (visibleCore != null)
        {
            lines.Add(CreateBoundarySegment(
                anchorAtTop,
                anchorStartY,
                anchorEndY,
                visibleCore.GetUpperSurfaceOffset(),
                definition,
                sectionLength,
                SectionLineRole.Structural));
            lines.Add(CreateBoundarySegment(
                anchorAtTop,
                anchorStartY,
                anchorEndY,
                visibleCore.GetLowerSurfaceOffset(),
                definition,
                sectionLength,
                SectionLineRole.Structural));
        }

        var topFinish = definition.Sections
            .Where(layer => !layer.IsCore && layer.VisibleInSection && layer.Side == SlabLayerSide.Top)
            .OrderByDescending(layer => layer.GetUpperSurfaceOffset())
            .FirstOrDefault();
        if (topFinish != null)
        {
            lines.AddRange(CreateFinishBoundaryLines(
                definition,
                anchorAtTop,
                anchorStartY,
                anchorEndY + finishSlopeDelta,
                sectionLength,
                wallIntervals,
                topFinish.GetUpperSurfaceOffset()));
        }

        var bottomFinish = definition.Sections
            .Where(layer => !layer.IsCore && layer.VisibleInSection && layer.Side == SlabLayerSide.Bottom)
            .OrderBy(layer => layer.GetLowerSurfaceOffset())
            .FirstOrDefault();
        if (bottomFinish != null)
        {
            lines.AddRange(CreateFinishBoundaryLines(
                definition,
                anchorAtTop,
                anchorStartY,
                anchorEndY + finishSlopeDelta,
                sectionLength,
                wallIntervals,
                bottomFinish.GetLowerSurfaceOffset()));
        }

        return lines;
    }

    private static IEnumerable<SectionLineSegment> CreateFinishBoundaryLines(
        BoundaryTemplateDefinition definition,
        bool anchorAtTop,
        double anchorStartY,
        double anchorEndY,
        double sectionLength,
        IReadOnlyList<(double StartX, double EndX)> wallIntervals,
        double layerOffset)
    {
        var line = CreateBoundaryLine(
            anchorAtTop,
            anchorStartY,
            anchorEndY,
            layerOffset,
            definition,
            sectionLength);

        return definition.Template.WallJunctionMode == SlabWallJunctionMode.StopAtWallFace
            ? ClipAtWallFaces(line, wallIntervals)
                .Select(clippedLine => new SectionLineSegment
                {
                    Line = clippedLine,
                    Role = SectionLineRole.Finish
                })
                .ToArray()
            : new[]
            {
                new SectionLineSegment
                {
                    Line = line,
                    Role = SectionLineRole.Finish
                }
            };
    }

    private static SectionLineSegment CreateBoundarySegment(
        bool anchorAtTop,
        double anchorStartY,
        double anchorEndY,
        double layerOffset,
        BoundaryTemplateDefinition definition,
        double sectionLength,
        SectionLineRole role)
    {
        return new SectionLineSegment
        {
            Line = CreateBoundaryLine(anchorAtTop, anchorStartY, anchorEndY, layerOffset, definition, sectionLength),
            Role = role
        };
    }

    private static Line3D CreateBoundaryLine(
        bool anchorAtTop,
        double anchorStartY,
        double anchorEndY,
        double layerOffset,
        BoundaryTemplateDefinition definition,
        double sectionLength)
    {
        var referenceOffset = anchorAtTop ? definition.TopmostOffset : definition.BottommostOffset;
        var delta = layerOffset - referenceOffset;
        return new Line3D(
            new Point3D(0, anchorStartY + delta, 0),
            new Point3D(sectionLength, anchorEndY + delta, 0));
    }

    private static IEnumerable<Line3D> ClipAtWallFaces(Line3D line, IReadOnlyList<(double StartX, double EndX)> wallIntervals)
    {
        if (wallIntervals.Count == 0)
        {
            yield return line;
            yield break;
        }

        var segments = new List<(double Start, double End)> { (line.Start.X, line.End.X) };
        foreach (var interval in wallIntervals)
        {
            var next = new List<(double Start, double End)>();
            foreach (var segment in segments)
            {
                var segStart = Math.Min(segment.Start, segment.End);
                var segEnd = Math.Max(segment.Start, segment.End);
                var clipStart = Math.Max(segStart, interval.StartX);
                var clipEnd = Math.Min(segEnd, interval.EndX);

                if (clipEnd <= clipStart + 1e-6)
                {
                    next.Add(segment);
                    continue;
                }

                if (clipStart > segStart + 1e-6)
                {
                    next.Add((segStart, clipStart));
                }

                if (clipEnd < segEnd - 1e-6)
                {
                    next.Add((clipEnd, segEnd));
                }
            }

            segments = next;
        }

        foreach (var segment in segments.Where(segment => segment.End - segment.Start > 1e-6))
        {
            var startY = InterpolateY(line, segment.Start);
            var endY = InterpolateY(line, segment.End);
            yield return new Line3D(
                new Point3D(segment.Start, startY, 0),
                new Point3D(segment.End, endY, 0));
        }
    }

    private static double InterpolateY(Line3D line, double x)
    {
        if (Math.Abs(line.End.X - line.Start.X) <= 1e-9)
        {
            return line.Start.Y;
        }

        var t = (x - line.Start.X) / (line.End.X - line.Start.X);
        return line.Start.Y + (line.End.Y - line.Start.Y) * t;
    }

    private BoundaryTemplateDefinition ResolveBoundaryDefinition(
        FloorConfig floor,
        string boundaryName,
        BoundarySlabConfig config,
        Func<SlabAssemblyTemplate> createLegacyTemplate,
        bool logResolution = false)
    {
        SlabAssemblyTemplate template;
        var usedLegacyTemplate = string.IsNullOrWhiteSpace(config.TemplateId);
        if (usedLegacyTemplate)
        {
            template = createLegacyTemplate();
        }
        else
        {
            template = _slabTemplateCatalog.GetById(config.TemplateId)
                ?? throw CreateMissingTemplateException(floor.Name, boundaryName, config.TemplateId);
        }

        var element = _slabAssemblyBuilder.Build(
            new CoreSlabArea
            {
                TemplateId = template.TemplateId,
                CoreTopElevation = 0
            },
            template);

        var topmostOffset = element.LayerSections.Max(layer => Math.Max(layer.TopOffset, layer.BottomOffset));
        var bottommostOffset = element.LayerSections.Min(layer => Math.Min(layer.TopOffset, layer.BottomOffset));
        var coreLayer = element.LayerSections.FirstOrDefault(layer => layer.IsCore);
        var coreTopOffset = coreLayer != null
            ? Math.Max(coreLayer.TopOffset, coreLayer.BottomOffset)
            : topmostOffset;
        var coreBottomOffset = coreLayer != null
            ? Math.Min(coreLayer.TopOffset, coreLayer.BottomOffset)
            : bottommostOffset;

        var definition = new BoundaryTemplateDefinition(
            template,
            config,
            element.LayerSections.Select(layer => new BoundaryLayerDefinition(
                layer.Name,
                layer.TopOffset,
                layer.BottomOffset,
                layer.VisibleInSection,
                layer.IsCore,
                layer.Side)).ToList(),
            topmostOffset,
            bottommostOffset,
            coreTopOffset,
            coreBottomOffset,
            topmostOffset - bottommostOffset,
            bottommostOffset - topmostOffset,
            coreTopOffset - topmostOffset,
            coreBottomOffset - topmostOffset,
            coreTopOffset - bottommostOffset,
            coreBottomOffset - bottommostOffset);

        if (logResolution)
        {
            _logger.LogInformation(
                "楼层 {FloorName} 的{BoundaryName}模板解析完成。RequestedTemplateId={RequestedTemplateId}, ResolvedTemplateId={ResolvedTemplateId}, ResolvedTemplateName={ResolvedTemplateName}, TopmostOffset={TopmostOffset:F2}, BottommostOffset={BottommostOffset:F2}, CoreTopOffset={CoreTopOffset:F2}, CoreBottomOffset={CoreBottomOffset:F2}, LegacyFallback={LegacyFallback}",
                floor.Name,
                boundaryName,
                string.IsNullOrWhiteSpace(config.TemplateId) ? null : config.TemplateId,
                template.TemplateId,
                template.TemplateName,
                definition.TopmostOffset,
                definition.BottommostOffset,
                definition.CoreTopOffset,
                definition.CoreBottomOffset,
                usedLegacyTemplate);
        }

        return definition;
    }

    private static BoundarySlabTemplateResolutionException CreateMissingTemplateException(
        string floorName,
        string boundaryName,
        string templateId)
    {
        var technicalMessage =
            $"楼层 {floorName} 的{boundaryName}已配置模板 {templateId}，但当前模板目录中找不到该模板。";
        var failure = new OperationFailure
        {
            Code = BoundarySlabTemplateMissingCode,
            Category = FailureCategory.UserInput,
            Stage = PipelineStage.FloorConfigLoad,
            Module = nameof(FloorVerticalProfileBuilder),
            UserMessage = $"{floorName} 的{boundaryName}模板不存在，请检查楼板模板配置",
            TechnicalMessage = technicalMessage
        };

        return new BoundarySlabTemplateResolutionException(
            floorName,
            boundaryName,
            templateId,
            failure);
    }

    private static SlabAssemblyTemplate CreateLegacyBottomTemplate(FloorConfig floor)
    {
        return new SlabAssemblyTemplate
        {
            TemplateId = "legacy-bottom-boundary",
            TemplateName = "LegacyBottomBoundary",
            WallJunctionMode = SlabWallJunctionMode.StopAtWallFace,
            CoreRule = new SlabCoreRule
            {
                Name = "结构底板",
                Thickness = Math.Max(floor.BottomSlabThickness, 1),
                MaterialOrCategory = "结构",
                VisibleInSection = true
            }
        };
    }

    private static SlabAssemblyTemplate CreateLegacyTopTemplate(FloorConfig floor)
    {
        var template = new SlabAssemblyTemplate
        {
            TemplateId = "legacy-top-boundary",
            TemplateName = "LegacyTopBoundary",
            WallJunctionMode = SlabWallJunctionMode.StopAtWallFace,
            CoreRule = new SlabCoreRule
            {
                Name = "结构顶板",
                Thickness = Math.Max(floor.TopSlabThickness, 1),
                MaterialOrCategory = "结构",
                VisibleInSection = true
            }
        };

        if (floor.FinishThickness > 0)
        {
            template.TopLayers.Add(new SlabLayerRule
            {
                Name = "装修面层",
                Side = SlabLayerSide.Top,
                Order = 1,
                Thickness = floor.FinishThickness,
                MaterialOrCategory = "装修",
                VisibleInSection = true
            });
        }

        return template;
    }

    private sealed record BoundaryTemplateDefinition(
        SlabAssemblyTemplate Template,
        BoundarySlabConfig Config,
        IReadOnlyList<BoundaryLayerDefinition> Sections,
        double TopmostOffset,
        double BottommostOffset,
        double CoreTopOffset,
        double CoreBottomOffset,
        double TopmostOffsetFromBottom,
        double BottommostOffsetFromTop,
        double CoreTopOffsetFromTop,
        double CoreBottomOffsetFromTop,
        double CoreTopOffsetFromBottom,
        double CoreBottomOffsetFromBottom);

    private readonly record struct BoundarySlope(double Value, bool TargetsFinishLayer);

    private sealed record BoundaryLayerDefinition(
        string Name,
        double TopOffset,
        double BottomOffset,
        bool VisibleInSection,
        bool IsCore,
        SlabLayerSide? Side)
    {
        public double GetUpperSurfaceOffset() => Math.Max(TopOffset, BottomOffset);
        public double GetLowerSurfaceOffset() => Math.Min(TopOffset, BottomOffset);
    }
}

public sealed class BoundarySlabTemplateResolutionException : ToolkitException
{
    public BoundarySlabTemplateResolutionException(
        string floorName,
        string boundaryName,
        string templateId,
        OperationFailure failure)
        : base(failure)
    {
        FloorName = floorName;
        BoundaryName = boundaryName;
        TemplateId = templateId;
    }

    public string FloorName { get; }
    public string BoundaryName { get; }
    public string TemplateId { get; }
}
