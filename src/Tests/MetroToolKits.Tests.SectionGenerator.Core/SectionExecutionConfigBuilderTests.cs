using System.Reflection;
using FluentAssertions;
using MetroToolKits.Foundation.Building.Types;
using MetroToolKits.SectionGenerator.App.Models;
using MetroToolKits.SectionGenerator.App.UseCases;
using MetroToolKits.SectionGenerator.Core.Sections;

namespace MetroToolKits.Tests.SectionGenerator.Core;

public class SectionExecutionConfigBuilderTests
{
    [Fact]
    public void TryBuild_WhenGlobalSlopeSettingsEnabled_GlobalValuesTakePriorityInExecutionDocument()
    {
        var sourceDocument = new LoadedSectionConfig
        {
            Config = new SectionConfig
            {
                AlignmentBaseFloorName = "F1",
                GlobalSlopeEnabled = true,
                GlobalSlopeValue = 0.02,
                GlobalSlopeTarget = "FinishLayer",
                GlobalTopSlopeEnabled = true,
                GlobalTopSlopeValue = 0.03,
                GlobalTopSlopeTarget = "StructuralSlab",
                GlobalBottomSlopeEnabled = true,
                GlobalBottomSlopeValue = 0.01,
                GlobalBottomSlopeTarget = "BottomSurface",
                Floors =
                [
                    new FloorConfig
                    {
                        Name = "F1",
                        Height = 5200,
                        HasSlope = true,
                        SlopeValue = 0.05,
                        SlopeTarget = "LegacyLocal",
                        TopBoundarySlab = new BoundarySlabConfig
                        {
                            TemplateId = "TOP-1",
                            SlopeEnabled = true,
                            SlopeValue = 0.06,
                            SlopeTarget = "LocalTop"
                        },
                        BottomBoundarySlab = new BoundarySlabConfig
                        {
                            TemplateId = "BOT-1",
                            SlopeEnabled = true,
                            SlopeValue = 0.07,
                            SlopeTarget = "LocalBottom"
                        }
                    }
                ]
            }
        };

        var (success, executionDocument, errorMessage) = InvokeTryBuild(sourceDocument, Array.Empty<string>(), "F1");

        success.Should().BeTrue();
        errorMessage.Should().BeEmpty();

        var floor = executionDocument.Config.Floors.Should().ContainSingle().Subject;
        floor.HasSlope.Should().BeFalse("图纸级全局坡度开启后，楼层 legacy 坡度不应再参与执行态");
        floor.TopBoundarySlab.SlopeEnabled.Should().BeTrue();
        floor.TopBoundarySlab.SlopeValue.Should().BeApproximately(0.03, 0.000001);
        floor.TopBoundarySlab.SlopeTarget.Should().Be("StructuralSlab");
        floor.BottomBoundarySlab.SlopeEnabled.Should().BeTrue();
        floor.BottomBoundarySlab.SlopeValue.Should().BeApproximately(0.01, 0.000001);
        floor.BottomBoundarySlab.SlopeTarget.Should().Be("BottomSurface");
    }

    [Fact]
    public void TryBuild_WhenGlobalSlopeSettingsDisabled_PreservesFloorLevelSlopeSettings()
    {
        var sourceDocument = new LoadedSectionConfig
        {
            Config = new SectionConfig
            {
                AlignmentBaseFloorName = "F1",
                GlobalSlopeEnabled = false,
                GlobalTopSlopeEnabled = false,
                GlobalBottomSlopeEnabled = false,
                Floors =
                [
                    new FloorConfig
                    {
                        Name = "F1",
                        Height = 5200,
                        HasSlope = true,
                        SlopeValue = 0.015,
                        SlopeTarget = "LegacyLocal",
                        TopBoundarySlab = new BoundarySlabConfig
                        {
                            TemplateId = "TOP-1",
                            SlopeEnabled = true,
                            SlopeValue = 0.02,
                            SlopeTarget = "LocalTop"
                        },
                        BottomBoundarySlab = new BoundarySlabConfig
                        {
                            TemplateId = "BOT-1",
                            SlopeEnabled = true,
                            SlopeValue = 0.01,
                            SlopeTarget = "LocalBottom"
                        }
                    }
                ]
            }
        };

        var (success, executionDocument, errorMessage) = InvokeTryBuild(sourceDocument, Array.Empty<string>(), "F1");

        success.Should().BeTrue();
        errorMessage.Should().BeEmpty();

        var floor = executionDocument.Config.Floors.Should().ContainSingle().Subject;
        floor.HasSlope.Should().BeTrue();
        floor.SlopeValue.Should().BeApproximately(0.015, 0.000001);
        floor.SlopeTarget.Should().Be("LegacyLocal");
        floor.TopBoundarySlab.SlopeEnabled.Should().BeTrue();
        floor.TopBoundarySlab.SlopeValue.Should().BeApproximately(0.02, 0.000001);
        floor.TopBoundarySlab.SlopeTarget.Should().Be("LocalTop");
        floor.BottomBoundarySlab.SlopeEnabled.Should().BeTrue();
        floor.BottomBoundarySlab.SlopeValue.Should().BeApproximately(0.01, 0.000001);
        floor.BottomBoundarySlab.SlopeTarget.Should().Be("LocalBottom");
    }

    [Fact]
    public void TryBuild_WhenOnlyLegacyGlobalSlopeEnabled_OnlyTopBoundaryUsesCompatibilityFallback()
    {
        var sourceDocument = new LoadedSectionConfig
        {
            Config = new SectionConfig
            {
                AlignmentBaseFloorName = "F1",
                GlobalSlopeEnabled = true,
                GlobalSlopeValue = 0.02,
                GlobalSlopeTarget = "FinishLayer",
                GlobalTopSlopeEnabled = false,
                GlobalBottomSlopeEnabled = false,
                Floors =
                [
                    new FloorConfig
                    {
                        Name = "F1",
                        Height = 5200,
                        HasSlope = true,
                        SlopeValue = 0.03,
                        SlopeTarget = "LegacyLocal",
                        TopBoundarySlab = new BoundarySlabConfig
                        {
                            TemplateId = "TOP-1",
                            SlopeEnabled = false,
                            SlopeValue = 0,
                            SlopeTarget = "LocalTop"
                        },
                        BottomBoundarySlab = new BoundarySlabConfig
                        {
                            TemplateId = "BOT-1",
                            SlopeEnabled = false,
                            SlopeValue = 0,
                            SlopeTarget = "LocalBottom"
                        }
                    }
                ]
            }
        };

        var (success, executionDocument, errorMessage) = InvokeTryBuild(sourceDocument, Array.Empty<string>(), "F1");

        success.Should().BeTrue();
        errorMessage.Should().BeEmpty();

        var floor = executionDocument.Config.Floors.Should().ContainSingle().Subject;
        floor.HasSlope.Should().BeFalse("legacy 全局坡度开启后，楼层兼容坡度不应继续参与执行态");
        floor.TopBoundarySlab.SlopeEnabled.Should().BeTrue("legacy 全局坡度兼容回退只作用于顶边界板");
        floor.TopBoundarySlab.SlopeValue.Should().BeApproximately(0.02, 0.000001);
        floor.TopBoundarySlab.SlopeTarget.Should().Be("FinishLayer");
        floor.BottomBoundarySlab.SlopeEnabled.Should().BeFalse("底边界板不应误用 legacy 全局坡度");
        floor.BottomBoundarySlab.SlopeTarget.Should().Be("LocalBottom");
    }

    private static (bool Success, LoadedSectionConfig ExecutionDocument, string ErrorMessage) InvokeTryBuild(
        LoadedSectionConfig sourceDocument,
        IReadOnlyList<string> includedFloorNames,
        string? targetFloorName)
    {
        var builderType = typeof(GenerateSectionUseCase).Assembly
            .GetType("MetroToolKits.SectionGenerator.App.UseCases.SectionExecutionConfigBuilder");
        builderType.Should().NotBeNull();

        var tryBuildMethod = builderType!.GetMethod(
            "TryBuild",
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        tryBuildMethod.Should().NotBeNull();

        object?[] arguments =
        [
            sourceDocument,
            includedFloorNames,
            targetFloorName,
            null,
            null
        ];

        var success = (bool)tryBuildMethod!.Invoke(null, arguments)!;
        var executionDocument = arguments[3].Should().BeOfType<LoadedSectionConfig>().Subject;
        var errorMessage = arguments[4].Should().BeOfType<string>().Subject;
        return (success, executionDocument, errorMessage);
    }
}
