using FluentAssertions;
using MetroToolKits.Foundation.Building.Types;
using MetroToolKits.Foundation.Core.Geometry;
using MetroToolKits.SectionGenerator.App.Abstractions;
using MetroToolKits.SectionGenerator.App.Models;
using MetroToolKits.SectionGenerator.App.Support;
using MetroToolKits.SectionGenerator.Contracts.Workbench;
using MetroToolKits.SectionGenerator.Core.Sections;
using NSubstitute;

namespace MetroToolKits.Tests.SectionGenerator.Core;

public class FloorConfigDocumentAssemblerTests
{
    private readonly ISlabAssemblyTemplateCatalog _catalog = Substitute.For<ISlabAssemblyTemplateCatalog>();
    private readonly IFloorConfigDocumentAssembler _assembler;

    public FloorConfigDocumentAssemblerTests()
    {
        _catalog.GetById(Arg.Any<string>()).Returns(callInfo => null);
        _assembler = new FloorConfigDocumentAssembler(_catalog);
    }

    [Fact]
    public void Assemble_WhenSingleFloorAndBaseMissing_UsesDefaultBaseFloor()
    {
        var document = CreateDocument(new SectionConfig
        {
            Floors =
            [
                new FloorConfig
                {
                    Name = "F1",
                    AlignmentPoints = CreateAlignmentPoints(),
                    ScopeBounds = CreateValidScope()
                }
            ]
        });

        var dto = _assembler.Assemble(document);

        dto.SelectedBaseFloorName.Should().Be("F1");
        dto.FloorSummaries.Should().ContainSingle();
        dto.FloorSummaries[0].IsBaseFloor.Should().BeTrue();
        dto.FloorSummaries[0].StatusText.Should().Contain("默认基准层");
    }

    [Fact]
    public void Assemble_WhenMultiFloorBaseMissing_ReportsDocumentIssue()
    {
        var document = CreateDocument(new SectionConfig
        {
            Floors =
            [
                new FloorConfig { Name = "F1" },
                new FloorConfig { Name = "F2" }
            ]
        });

        var dto = _assembler.Assemble(document);

        dto.SelectedBaseFloorName.Should().BeEmpty();
        dto.Issues.Should().ContainSingle(issue => issue.Code == "AlignmentBaseFloorMissing" &&
                                                    issue.Severity == ValidationSeverityDto.Error);
    }

    [Fact]
    public void Assemble_WhenMultipleFloors_BuildsStackRoleText()
    {
        var document = CreateDocument(new SectionConfig
        {
            Floors =
            [
                new FloorConfig { Name = "F1" },
                new FloorConfig { Name = "F2" },
                new FloorConfig { Name = "F3" }
            ]
        });

        var dto = _assembler.Assemble(document);

        dto.FloorSummaries.Select(summary => $"{summary.FloorName} · {summary.StackRoleText}")
            .Should()
            .Equal("F1 · 底层", "F2 · 中间层", "F3 · 顶层");
    }

    [Fact]
    public void Assemble_WhenBaseFloorMissingThreePoints_ReportsAlignmentIssue()
    {
        var document = CreateDocument(new SectionConfig
        {
            AlignmentBaseFloorName = "F1",
            Floors =
            [
                new FloorConfig
                {
                    Name = "F1",
                    AlignmentPoints = [new Point3D(0, 0, 0), new Point3D(10, 0, 0)]
                },
                new FloorConfig
                {
                    Name = "F2",
                    AlignmentPoints = CreateAlignmentPoints()
                }
            ]
        });

        var dto = _assembler.Assemble(document);
        var baseFloor = dto.FloorDetails.Single(detail => detail.FloorName == "F1");

        baseFloor.IsBaseFloor.Should().BeTrue();
        baseFloor.Alignment.PointCount.Should().Be(2);
        baseFloor.Alignment.IsValid.Should().BeFalse();
        baseFloor.MissingRequirements.Should().Contain(issue => issue.Code == "FloorAlignmentMissingOrInvalid");
    }

    [Fact]
    public void Assemble_WhenScopeBoundsInvalid_ReportsInvalidScope()
    {
        var document = CreateDocument(new SectionConfig
        {
            Floors =
            [
                new FloorConfig
                {
                    Name = "F1",
                    AlignmentPoints = CreateAlignmentPoints(),
                    ScopeBounds = new ScopeBounds2D
                    {
                        MinX = 0,
                        MinY = 0,
                        MaxX = 0,
                        MaxY = 100
                    }
                }
            ]
        });

        var dto = _assembler.Assemble(document);
        var floor = dto.FloorDetails.Single();

        floor.Scope.HasValue.Should().BeTrue();
        floor.Scope.IsValid.Should().BeFalse();
        floor.Scope.StatusText.Should().Be("范围无效");
        floor.MissingRequirements.Should().Contain(issue => issue.Code == "FloorScopeInvalid");
    }

    [Fact]
    public void Assemble_WhenBoundaryTemplatesBound_ResolvesTemplateDisplay()
    {
        _catalog.GetById("TOP-1").Returns(new SlabAssemblyTemplate
        {
            TemplateId = "TOP-1",
            TemplateName = "顶板模板",
            CoreRule = new SlabCoreRule { Thickness = 180 },
            TopLayers =
            [
                new SlabLayerRule { Thickness = 20 },
                new SlabLayerRule { Thickness = 30 }
            ]
        });
        _catalog.GetById("BOT-1").Returns(new SlabAssemblyTemplate
        {
            TemplateId = "BOT-1",
            TemplateName = "底板模板",
            CoreRule = new SlabCoreRule { Thickness = 220 },
            BottomLayers =
            [
                new SlabLayerRule { Thickness = 15 }
            ]
        });

        var document = CreateDocument(new SectionConfig
        {
            Floors =
            [
                new FloorConfig
                {
                    Name = "F1",
                    TopBoundarySlab = new BoundarySlabConfig { TemplateId = "TOP-1" },
                    BottomBoundarySlab = new BoundarySlabConfig { TemplateId = "BOT-1" }
                }
            ]
        });

        var dto = _assembler.Assemble(document);
        var floor = dto.FloorDetails.Single();

        floor.TopBoundarySlab.TemplateExists.Should().BeTrue();
        floor.TopBoundarySlab.TemplateName.Should().Be("顶板模板");
        floor.TopBoundarySlab.StructuralThickness.Should().Be(180);
        floor.TopBoundarySlab.FinishThickness.Should().Be(50);

        floor.BottomBoundarySlab.TemplateExists.Should().BeTrue();
        floor.BottomBoundarySlab.TemplateName.Should().Be("底板模板");
        floor.BottomBoundarySlab.StructuralThickness.Should().Be(220);
        floor.BottomBoundarySlab.FinishThickness.Should().Be(15);
    }

    [Fact]
    public void Assemble_WhenTemplateMissing_ShowsMissingTemplate()
    {
        var document = CreateDocument(new SectionConfig
        {
            Floors =
            [
                new FloorConfig
                {
                    Name = "F1",
                    TopBoundarySlab = new BoundarySlabConfig { TemplateId = "MISSING-TOP" }
                }
            ]
        });

        var dto = _assembler.Assemble(document);
        var floor = dto.FloorDetails.Single();

        floor.TopBoundarySlab.TemplateExists.Should().BeFalse();
        floor.TopBoundarySlab.TemplateId.Should().Be("MISSING-TOP");
        floor.TopBoundarySlab.DisplayText.Should().Be("模板不存在");
    }

    [Fact]
    public void Assemble_WhenGlobalSlopeEnabledOrDisabled_MapsStatus()
    {
        var enabledDocument = CreateDocument(new SectionConfig
        {
            GlobalSlopeEnabled = true,
            GlobalSlopeValue = 0.0025,
            GlobalSlopeTarget = "FinishLayer",
            Floors = [new FloorConfig { Name = "F1" }]
        });

        var disabledDocument = CreateDocument(new SectionConfig
        {
            GlobalSlopeEnabled = false,
            GlobalSlopeValue = 0.0025,
            GlobalSlopeTarget = "StructuralSlab",
            Floors = [new FloorConfig { Name = "F1" }]
        });

        var enabledDto = _assembler.Assemble(enabledDocument);
        var disabledDto = _assembler.Assemble(disabledDocument);

        enabledDto.GlobalSlope.Enabled.Should().BeTrue();
        enabledDto.GlobalSlope.SlopePercent.Should().Be(0.25);
        enabledDto.GlobalSlope.Target.Should().Be("FinishLayer");
        enabledDto.GlobalSlope.StatusText.Should().Contain("已启用");

        disabledDto.GlobalSlope.Enabled.Should().BeFalse();
        disabledDto.GlobalSlope.StatusText.Should().Be("未启用");
    }

    private static LoadedSectionConfig CreateDocument(SectionConfig config)
        => new()
        {
            Config = config,
            RuntimeState = new SectionConfigRuntimeState
            {
                Source = SectionConfigStorageSource.EmbeddedDwg,
                DrawingDisplayName = "Test.dwg",
                IsCurrentDrawingSaved = true
            }
        };

    private static List<Point3D> CreateAlignmentPoints()
        =>
        [
            new Point3D(0, 0, 0),
            new Point3D(10, 0, 0),
            new Point3D(0, 10, 0)
        ];

    private static ScopeBounds2D CreateValidScope()
        => new()
        {
            MinX = 0,
            MinY = 0,
            MaxX = 100,
            MaxY = 100
        };
}
