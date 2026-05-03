using MetroToolKits.Foundation.Building.Types;
using MetroToolKits.Foundation.Core.Diagnostics;
using MetroToolKits.Foundation.Core.Geometry;
using MetroToolKits.SectionGenerator.App.Abstractions;
using MetroToolKits.SectionGenerator.App.Models;
using MetroToolKits.SectionGenerator.App.ViewModels;
using MetroToolKits.SectionGenerator.Contracts.Floors;
using MetroToolKits.SectionGenerator.Contracts.Workbench;
using MetroToolKits.SectionGenerator.Core.Sections;

namespace MetroToolKits.SectionGenerator.App.Support;

public sealed class FloorConfigDocumentAssembler : IFloorConfigDocumentAssembler
{
    private readonly ISlabAssemblyTemplateCatalog _slabTemplateCatalog;

    public FloorConfigDocumentAssembler(ISlabAssemblyTemplateCatalog slabTemplateCatalog)
    {
        _slabTemplateCatalog = slabTemplateCatalog;
    }

    public FloorConfigDocumentDto Assemble(LoadedSectionConfig document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var config = document.Config ?? new SectionConfig();
        var floors = config.Floors ?? [];
        var effectiveBaseFloorName = ResolveEffectiveBaseFloorName(config, floors);
        var baseFloorMissing = floors.Count > 1 && string.IsNullOrWhiteSpace(effectiveBaseFloorName);
        var documentIssues = BuildDocumentIssues(config, floors, effectiveBaseFloorName);
        var floorDetails = floors.Select(floor => BuildFloorDetail(floor, effectiveBaseFloorName, floors.Count > 1)).ToArray();

        return new FloorConfigDocumentDto
        {
            Drawing = BuildDrawingStatus(document.RuntimeState),
            GlobalSlope = BuildGlobalSlope(config),
            SelectedBaseFloorName = effectiveBaseFloorName,
            ConfigSourceStatusText = BuildConfigSourceStatus(document.RuntimeState),
            Issues = documentIssues,
            FloorSummaries = floorDetails.Select((detail, index) => BuildFloorSummary(
                detail,
                index,
                floors.Count,
                floors.Count == 1,
                string.IsNullOrWhiteSpace(config.AlignmentBaseFloorName),
                baseFloorMissing)).ToArray(),
            FloorDetails = floorDetails
        };
    }

    private FloorDetailDto BuildFloorDetail(FloorConfig floor, string effectiveBaseFloorName, bool isMultiFloor)
    {
        var isBaseFloor = string.Equals(floor.Name, effectiveBaseFloorName, StringComparison.OrdinalIgnoreCase);
        var missingRequirements = new List<ValidationIssueDto>();

        var alignment = BuildAlignment(floor.AlignmentPoints);
        if (!alignment.IsValid)
        {
            missingRequirements.Add(new ValidationIssueDto
            {
                Code = "FloorAlignmentMissingOrInvalid",
                Message = alignment.StatusText,
                Severity = ValidationSeverityDto.Warning,
                Target = floor.Name
            });
        }

        var scope = BuildScope(floor.ScopeBounds);
        if (!scope.HasValue)
        {
            missingRequirements.Add(new ValidationIssueDto
            {
                Code = "FloorScopeMissing",
                Message = "缺范围",
                Severity = isBaseFloor && isMultiFloor ? ValidationSeverityDto.Error : ValidationSeverityDto.Warning,
                Target = floor.Name
            });
        }
        else if (!scope.IsValid)
        {
            missingRequirements.Add(new ValidationIssueDto
            {
                Code = "FloorScopeInvalid",
                Message = "整层范围无效",
                Severity = isBaseFloor && isMultiFloor ? ValidationSeverityDto.Error : ValidationSeverityDto.Warning,
                Target = floor.Name
            });
        }

        return new FloorDetailDto
        {
            FloorName = floor.Name,
            IsBaseFloor = isBaseFloor,
            Alignment = alignment,
            Scope = scope,
            TopBoundarySlab = BuildBoundarySlab(floor.TopBoundarySlab, SlabLayerSide.Top),
            BottomBoundarySlab = BuildBoundarySlab(floor.BottomBoundarySlab, SlabLayerSide.Bottom),
            TopSlope = BuildFloorSlope(floor.TopBoundarySlab),
            BottomSlope = BuildFloorSlope(floor.BottomBoundarySlab),
            MissingRequirements = missingRequirements
        };
    }

    private static FloorSummaryDto BuildFloorSummary(
        FloorDetailDto detail,
        int floorIndex,
        int floorCount,
        bool isSingleFloor,
        bool baseFloorWasImplicit,
        bool baseFloorMissing)
    {
        var baseLabel = detail.IsBaseFloor
            ? isSingleFloor && baseFloorWasImplicit
                ? "默认基准层"
                : "基准层"
            : string.Empty;

        var primaryStatus = baseFloorMissing
            ? "缺少基准层"
            : detail.MissingRequirements.FirstOrDefault()?.Message ?? "已配置";
        var statusText = string.IsNullOrWhiteSpace(baseLabel)
            ? primaryStatus
            : $"{baseLabel} · {primaryStatus}";

        return new FloorSummaryDto
        {
            FloorName = detail.FloorName,
            StackRoleText = FloorStackUiStateBuilder.Create(floorIndex, floorCount).RoleText,
            IsBaseFloor = detail.IsBaseFloor,
            HasBlockingIssues = baseFloorMissing || detail.MissingRequirements.Count > 0,
            StatusText = statusText
        };
    }

    private BoundarySlabDto BuildBoundarySlab(BoundarySlabConfig config, SlabLayerSide preferredSide)
    {
        if (string.IsNullOrWhiteSpace(config.TemplateId))
        {
            return new BoundarySlabDto
            {
                TemplateId = string.Empty,
                TemplateExists = false,
                DisplayText = "未绑定模板"
            };
        }

        var template = _slabTemplateCatalog.GetById(config.TemplateId);
        if (template == null)
        {
            return new BoundarySlabDto
            {
                TemplateId = config.TemplateId,
                TemplateExists = false,
                DisplayText = "模板不存在"
            };
        }

        var finishThickness = preferredSide == SlabLayerSide.Top
            ? template.TopLayers.Sum(layer => layer.Thickness)
            : template.BottomLayers.Sum(layer => layer.Thickness);

        var displayText = preferredSide == SlabLayerSide.Top
            ? $"{template.TemplateName}（结构 {template.CoreRule.Thickness:F0}，顶侧附加层 {finishThickness:F0}）"
            : $"{template.TemplateName}（结构 {template.CoreRule.Thickness:F0}，底侧附加层 {finishThickness:F0}）";

        return new BoundarySlabDto
        {
            TemplateId = template.TemplateId,
            TemplateName = template.TemplateName,
            TemplateExists = true,
            StructuralThickness = template.CoreRule.Thickness,
            FinishThickness = finishThickness,
            DisplayText = displayText
        };
    }

    private static FloorAlignmentDto BuildAlignment(IReadOnlyList<Point3D>? points)
    {
        var pointCount = points?.Count ?? 0;
        if (pointCount == 0)
        {
            return new FloorAlignmentDto
            {
                PointCount = 0,
                IsValid = false,
                StatusText = "缺对齐点"
            };
        }

        if (FloorSectionLineTransformer.TryValidateAlignmentPoints(points, out _))
        {
            return new FloorAlignmentDto
            {
                PointCount = pointCount,
                IsValid = true,
                StatusText = "已设置（3点）"
            };
        }

        return new FloorAlignmentDto
        {
            PointCount = pointCount,
            IsValid = false,
            StatusText = pointCount == 3 ? "对齐点无效" : "对齐点不足"
        };
    }

    private static FloorScopeDto BuildScope(ScopeBounds2D? scopeBounds)
    {
        if (!scopeBounds.HasValue)
        {
            return new FloorScopeDto
            {
                HasValue = false,
                IsValid = false,
                StatusText = "缺范围"
            };
        }

        var bounds = scopeBounds.Value;
        return new FloorScopeDto
        {
            HasValue = true,
            IsValid = bounds.IsValid(),
            Bounds = new ScopeBoundsDto
            {
                MinX = bounds.MinX,
                MinY = bounds.MinY,
                MaxX = bounds.MaxX,
                MaxY = bounds.MaxY
            },
            StatusText = bounds.IsValid() ? "已设置范围" : "范围无效"
        };
    }

    private static FloorSlopeDto BuildFloorSlope(BoundarySlabConfig config)
        => new()
        {
            Enabled = config.SlopeEnabled,
            SlopePercent = config.SlopeValue * 100.0,
            Target = config.SlopeTarget,
            StatusText = config.SlopeEnabled
                ? $"已启用（{config.SlopeValue * 100.0:F2}%）"
                : "未启用"
        };

    private static GlobalSlopeDto BuildGlobalSlope(SectionConfig config)
        => new()
        {
            Enabled = config.GlobalSlopeEnabled,
            SlopePercent = config.GlobalSlopeValue * 100.0,
            Target = config.GlobalSlopeTarget,
            StatusText = config.GlobalSlopeEnabled
                ? $"已启用（{config.GlobalSlopeValue * 100.0:F2}%）"
                : "未启用"
        };

    private static DrawingStatusDto BuildDrawingStatus(SectionConfigRuntimeState runtimeState)
        => new()
        {
            DrawingDisplayName = runtimeState.DrawingDisplayName,
            StorageSource = runtimeState.Source switch
            {
                SectionConfigStorageSource.EmbeddedDwg => DrawingConfigSourceDto.EmbeddedDwg,
                SectionConfigStorageSource.TransientUnsavedDrawing => DrawingConfigSourceDto.TransientUnsavedDrawing,
                _ => DrawingConfigSourceDto.Missing
            },
            IsCurrentDrawingSaved = runtimeState.IsCurrentDrawingSaved,
            HasPersistedConfig = runtimeState.Source == SectionConfigStorageSource.EmbeddedDwg
        };

    private static string ResolveEffectiveBaseFloorName(SectionConfig config, IReadOnlyList<FloorConfig> floors)
    {
        if (!string.IsNullOrWhiteSpace(config.AlignmentBaseFloorName))
        {
            return config.AlignmentBaseFloorName;
        }

        return floors.Count == 1 ? floors[0].Name : string.Empty;
    }

    private static IReadOnlyList<ValidationIssueDto> BuildDocumentIssues(
        SectionConfig config,
        IReadOnlyList<FloorConfig> floors,
        string effectiveBaseFloorName)
    {
        if (floors.Count <= 1 || !string.IsNullOrWhiteSpace(effectiveBaseFloorName))
        {
            return Array.Empty<ValidationIssueDto>();
        }

        return
        [
            new ValidationIssueDto
            {
                Code = "AlignmentBaseFloorMissing",
                Message = "缺少基准层",
                Severity = ValidationSeverityDto.Error
            }
        ];
    }

    private static string BuildConfigSourceStatus(SectionConfigRuntimeState state)
    {
        return state.Source switch
        {
            SectionConfigStorageSource.EmbeddedDwg =>
                $"当前图纸 [{state.DrawingDisplayName}] 使用 DWG 内嵌楼层配置。",
            SectionConfigStorageSource.TransientUnsavedDrawing when state.IsCurrentDrawingSaved =>
                $"当前图纸 [{state.DrawingDisplayName}] 正在使用尚未写入 DWG 的临时楼层配置，请执行一次保存把配置写入图内。",
            SectionConfigStorageSource.TransientUnsavedDrawing =>
                $"当前图纸 [{state.DrawingDisplayName}] 尚未保存，楼层配置仅在本次会话有效，保存图纸后再保存配置才会写入 DWG。",
            _ when state.IsCurrentDrawingSaved =>
                $"当前图纸 [{state.DrawingDisplayName}] 尚未配置基准点/整层范围，保存后会直接写入该 DWG。",
            _ =>
                $"当前图纸 [{state.DrawingDisplayName}] 尚未建立图内楼层配置。"
        };
    }
}
