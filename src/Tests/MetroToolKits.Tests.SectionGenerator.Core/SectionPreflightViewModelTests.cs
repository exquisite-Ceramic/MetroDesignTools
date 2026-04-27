using FluentAssertions;
using MetroToolKits.SectionGenerator.App.ViewModels;
using MetroToolKits.SectionGenerator.Contracts.Preflight;

namespace MetroToolKits.Tests.SectionGenerator.Core;

public class SectionPreflightViewModelTests
{
    [Fact]
    public void Load_GroupsBlockingIssuesCorrectly()
    {
        var viewModel = new SectionPreflightViewModel();
        viewModel.Load(CreateReport(
            new SectionPreflightCheckItemDto
            {
                Key = "base-floor",
                Title = "基准层是否存在",
                Severity = SectionPreflightSeverityDto.Blocking,
                Summary = "多楼层模式下尚未选择基准层。",
                SuggestedActionText = "请打开楼层配置并指定一个基准层。",
                RelatedObjectName = "楼层配置",
                ActionTarget = SectionPreflightActionTargetDto.FloorConfig,
                SuggestedCommandTag = "FloorConfig"
            }));

        viewModel.BlockingChecks.Should().ContainSingle();
        viewModel.WarningChecks.Should().BeEmpty();
        viewModel.InfoChecks.Should().BeEmpty();
    }

    [Fact]
    public void Load_GroupsWarningIssuesCorrectly()
    {
        var viewModel = new SectionPreflightViewModel();
        viewModel.Load(CreateReport(
            new SectionPreflightCheckItemDto
            {
                Key = "skipped-floors",
                Title = "非基准层是否会被跳过",
                Severity = SectionPreflightSeverityDto.Warning,
                Summary = "F2 将被跳过。",
                SuggestedActionText = "请打开楼层配置，补齐这些楼层的对齐点或范围框。",
                RelatedObjectName = "F2",
                ActionTarget = SectionPreflightActionTargetDto.FloorConfig,
                SuggestedCommandTag = "FloorConfig",
                FloorName = "F2"
            }));

        viewModel.BlockingChecks.Should().BeEmpty();
        viewModel.WarningChecks.Should().ContainSingle();
        viewModel.InfoChecks.Should().BeEmpty();
    }

    [Fact]
    public void Load_GroupsInfoIssuesCorrectly()
    {
        var viewModel = new SectionPreflightViewModel();
        viewModel.Load(CreateReport(
            new SectionPreflightCheckItemDto
            {
                Key = "drawing",
                Title = "当前图纸状态",
                Severity = SectionPreflightSeverityDto.Info,
                Summary = "当前图纸已保存。",
                SuggestedActionText = "当前无需处理。",
                RelatedObjectName = "当前图纸",
                ActionTarget = SectionPreflightActionTargetDto.None,
                SuggestedCommandTag = string.Empty
            }));

        viewModel.BlockingChecks.Should().BeEmpty();
        viewModel.WarningChecks.Should().BeEmpty();
        viewModel.InfoChecks.Should().ContainSingle();
    }

    [Fact]
    public void Load_CanGenerateFalse_UsesBlockingSummary()
    {
        var viewModel = new SectionPreflightViewModel();
        viewModel.Load(CreateReport(
            new SectionPreflightCheckItemDto
            {
                Key = "base-floor",
                Title = "基准层是否存在",
                Severity = SectionPreflightSeverityDto.Blocking,
                Summary = "多楼层模式下尚未选择基准层。",
                SuggestedActionText = "请打开楼层配置并指定一个基准层。",
                RelatedObjectName = "楼层配置",
                ActionTarget = SectionPreflightActionTargetDto.FloorConfig,
                SuggestedCommandTag = "FloorConfig"
            },
            canGenerate: false,
            summaryText: "当前预检未通过，需先处理 1 个 Blocking 项。Warning 0，Info 0。"));

        viewModel.CanGenerate.Should().BeFalse();
        viewModel.SummaryText.Should().Contain("当前预检未通过");
    }

    [Fact]
    public void Load_CanGenerateTrue_UsesPassingSummary()
    {
        var viewModel = new SectionPreflightViewModel();
        viewModel.Load(CreateReport(
            new SectionPreflightCheckItemDto
            {
                Key = "drawing",
                Title = "当前图纸状态",
                Severity = SectionPreflightSeverityDto.Info,
                Summary = "当前图纸已保存。",
                SuggestedActionText = "当前无需处理。",
                RelatedObjectName = "当前图纸",
                ActionTarget = SectionPreflightActionTargetDto.None,
                SuggestedCommandTag = string.Empty
            },
            canGenerate: true,
            summaryText: "当前预检通过，可生成。Warning 0，Info 1。"));

        viewModel.CanGenerate.Should().BeTrue();
        viewModel.SummaryText.Should().Contain("当前预检通过");
    }

    [Fact]
    public void Load_PreservesSkippedFloorWarning()
    {
        var viewModel = new SectionPreflightViewModel();
        viewModel.Load(new SectionPreflightReportDto
        {
            CanGenerate = true,
            SummaryText = "当前预检通过，可生成。Warning 1，Info 0。",
            WarningCount = 1,
            Checks =
            [
                new SectionPreflightCheckItemDto
                {
                    Key = "skipped-floors",
                    Title = "非基准层是否会被跳过",
                    Severity = SectionPreflightSeverityDto.Warning,
                    Summary = "F2 将被跳过。",
                    SuggestedActionText = "请打开楼层配置，补齐这些楼层的对齐点或范围框。",
                    RelatedObjectName = "F2",
                    ActionTarget = SectionPreflightActionTargetDto.FloorConfig,
                    SuggestedCommandTag = "FloorConfig",
                    FloorName = "F2"
                }
            ],
            Floors =
            [
                new SectionPreflightFloorStatusDto
                {
                    FloorName = "F2",
                    WillBeSkipped = true,
                    AlignmentStatusText = "缺少对齐点。",
                    ScopeStatusText = "整层范围有效。",
                    BoundaryTemplateStatusText = "未显式指定模板，允许使用兼容回退。",
                    SkipReasonText = "缺少对齐点。"
                }
            ]
        });

        viewModel.WarningChecks.Should().ContainSingle();
        viewModel.Floors.Should().ContainSingle(floor =>
            floor.FloorName == "F2" &&
            floor.ParticipationText == "跳过" &&
            floor.SkipReasonText.Contains("缺少对齐点"));
    }

    [Fact]
    public void Load_PreservesSuggestedActionTargets()
    {
        var viewModel = new SectionPreflightViewModel();
        viewModel.Load(new SectionPreflightReportDto
        {
            CanGenerate = false,
            SummaryText = "当前预检未通过，需先处理 1 个 Blocking 项。Warning 1，Info 0。",
            BlockingCount = 1,
            WarningCount = 1,
            Checks =
            [
                new SectionPreflightCheckItemDto
                {
                    Key = "base-floor",
                    Title = "基准层是否存在",
                    Severity = SectionPreflightSeverityDto.Blocking,
                    Summary = "多楼层模式下尚未选择基准层。",
                    SuggestedActionText = "请打开楼层配置并指定一个基准层。",
                    RelatedObjectName = "楼层配置",
                    ActionTarget = SectionPreflightActionTargetDto.FloorConfig,
                    SuggestedCommandTag = "FloorConfig"
                },
                new SectionPreflightCheckItemDto
                {
                    Key = "readiness",
                    Title = "图层映射 / 构件识别状态",
                    Severity = SectionPreflightSeverityDto.Warning,
                    Summary = "当前尚未发现可识别构件。",
                    SuggestedActionText = "请检查图层映射或区域转换。",
                    RelatedObjectName = "图层映射",
                    ActionTarget = SectionPreflightActionTargetDto.LayerMapping,
                    SuggestedCommandTag = "LayerMapping"
                }
            ]
        });

        viewModel.BlockingChecks.Single().SuggestedCommandTag.Should().Be("FloorConfig");
        viewModel.BlockingChecks.Single().ActionTarget.Should().Be(SectionPreflightActionTargetDto.FloorConfig);
        viewModel.WarningChecks.Single().SuggestedCommandTag.Should().Be("LayerMapping");
        viewModel.WarningChecks.Single().ActionTarget.Should().Be(SectionPreflightActionTargetDto.LayerMapping);
    }

    private static SectionPreflightReportDto CreateReport(
        SectionPreflightCheckItemDto check,
        bool canGenerate = false,
        string summaryText = "当前预检未通过，需先处理 1 个 Blocking 项。Warning 0，Info 0。")
        => new()
        {
            CanGenerate = canGenerate,
            SummaryText = summaryText,
            BlockingCount = check.Severity == SectionPreflightSeverityDto.Blocking ? 1 : 0,
            WarningCount = check.Severity == SectionPreflightSeverityDto.Warning ? 1 : 0,
            InfoCount = check.Severity == SectionPreflightSeverityDto.Info ? 1 : 0,
            Checks = [check]
        };
}
