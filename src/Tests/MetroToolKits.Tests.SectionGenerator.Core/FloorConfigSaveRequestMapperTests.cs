using FluentAssertions;
using MetroToolKits.Foundation.Building.Types;
using MetroToolKits.Foundation.Core.Diagnostics;
using MetroToolKits.Foundation.Core.Geometry;
using MetroToolKits.SectionGenerator.App.Abstractions;
using MetroToolKits.SectionGenerator.App.Models;
using MetroToolKits.SectionGenerator.App.Support;
using MetroToolKits.SectionGenerator.Contracts.Common;
using MetroToolKits.SectionGenerator.Contracts.Floors;
using MetroToolKits.SectionGenerator.Core.Sections;

namespace MetroToolKits.Tests.SectionGenerator.Core;

public class FloorConfigSaveRequestMapperTests
{
    private readonly IFloorConfigSaveRequestMapper _mapper = new FloorConfigSaveRequestMapper();

    [Fact]
    public void ToRequest_WhenMultipleFloorsConfigured_PreservesOrderAndContent()
    {
        var document = CreateDocument(new SectionConfig
        {
            AlignmentBaseFloorName = "F2",
            Floors =
            [
                CreateFloor("F1", 5200),
                CreateFloor("F2", 5600)
            ]
        });

        var request = _mapper.ToRequest(document);

        request.AlignmentBaseFloorName.Should().Be("F2");
        request.Floors.Select(floor => floor.Name).Should().Equal("F1", "F2");
        request.Floors.Select(floor => floor.Height).Should().Equal(5200, 5600);
    }

    [Fact]
    public void ToRequest_PreservesBoundaryTemplateIds()
    {
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

        var request = _mapper.ToRequest(document);
        var floor = request.Floors.Single();

        floor.TopBoundarySlab.TemplateId.Should().Be("TOP-1");
        floor.BottomBoundarySlab.TemplateId.Should().Be("BOT-1");
    }

    [Fact]
    public void ToRequest_ConvertsGlobalSlopeValuesToPercent()
    {
        var document = CreateDocument(new SectionConfig
        {
            GlobalSlopeEnabled = true,
            GlobalSlopeValue = 0.02,
            GlobalSlopeTarget = "StructuralSlab",
            GlobalTopSlopeEnabled = true,
            GlobalTopSlopeValue = 0.015,
            GlobalTopSlopeTarget = "TopFinish",
            GlobalBottomSlopeEnabled = true,
            GlobalBottomSlopeValue = 0.01,
            GlobalBottomSlopeTarget = "BottomFinish",
            Floors = [CreateFloor("F1", 5200)]
        });

        var request = _mapper.ToRequest(document);

        request.GlobalSlopeEnabled.Should().BeTrue();
        request.GlobalSlopePercent.Should().Be(2.0);
        request.GlobalSlopeTarget.Should().Be("StructuralSlab");
        request.GlobalTopSlopeEnabled.Should().BeTrue();
        request.GlobalTopSlopePercent.Should().Be(1.5);
        request.GlobalTopSlopeTarget.Should().Be("TopFinish");
        request.GlobalBottomSlopeEnabled.Should().BeTrue();
        request.GlobalBottomSlopePercent.Should().Be(1.0);
        request.GlobalBottomSlopeTarget.Should().Be("BottomFinish");
    }

    [Fact]
    public void ToRequest_ConvertsFloorAndBoundarySlopesToPercent()
    {
        var document = CreateDocument(new SectionConfig
        {
            Floors =
            [
                new FloorConfig
                {
                    Name = "F1",
                    HasSlope = true,
                    SlopeValue = 0.03,
                    SlopeTarget = "LegacyStructural",
                    TopBoundarySlab = new BoundarySlabConfig
                    {
                        SlopeEnabled = true,
                        SlopeValue = 0.025,
                        SlopeTarget = "TopSurface"
                    },
                    BottomBoundarySlab = new BoundarySlabConfig
                    {
                        SlopeEnabled = true,
                        SlopeValue = 0.01,
                        SlopeTarget = "BottomSurface"
                    }
                }
            ]
        });

        var request = _mapper.ToRequest(document);
        var floor = request.Floors.Single();

        floor.LegacySlopeEnabled.Should().BeTrue();
        floor.LegacySlopePercent.Should().Be(3.0);
        floor.LegacySlopeTarget.Should().Be("LegacyStructural");
        floor.TopBoundarySlab.SlopeEnabled.Should().BeTrue();
        floor.TopBoundarySlab.SlopePercent.Should().Be(2.5);
        floor.TopBoundarySlab.SlopeTarget.Should().Be("TopSurface");
        floor.BottomBoundarySlab.SlopeEnabled.Should().BeTrue();
        floor.BottomBoundarySlab.SlopePercent.Should().Be(1.0);
        floor.BottomBoundarySlab.SlopeTarget.Should().Be("BottomSurface");
    }

    [Fact]
    public void ToRequest_PreservesAlignmentPointsAndScopeBounds()
    {
        var alignmentPoints = new[]
        {
            new Point3D(1, 2, 3),
            new Point3D(4, 5, 6),
            new Point3D(7, 8, 9)
        };
        var scopeBounds = new ScopeBounds2D
        {
            MinX = 10,
            MinY = 20,
            MaxX = 30,
            MaxY = 40
        };
        var document = CreateDocument(new SectionConfig
        {
            Floors =
            [
                new FloorConfig
                {
                    Name = "F1",
                    AlignmentPoints = alignmentPoints.ToList(),
                    ScopeBounds = scopeBounds
                },
                new FloorConfig
                {
                    Name = "F2",
                    ScopeBounds = null
                }
            ]
        });

        var request = _mapper.ToRequest(document);

        request.Floors[0].AlignmentPoints.Select(point => (point.X, point.Y, point.Z))
            .Should().Equal((1d, 2d, 3d), (4d, 5d, 6d), (7d, 8d, 9d));
        request.Floors[0].ScopeBounds.Should().NotBeNull();
        request.Floors[0].ScopeBounds!.MinX.Should().Be(10);
        request.Floors[0].ScopeBounds!.MaxY.Should().Be(40);
        request.Floors[1].ScopeBounds.Should().BeNull();
    }

    [Fact]
    public void ApplyToDocument_RebuildsFloorsInRequestOrderAndRestoresDecimalSlopes()
    {
        var outputConfig = new SectionOutputConfig();
        var runtimeState = new SectionConfigRuntimeState
        {
            Source = SectionConfigStorageSource.EmbeddedDwg,
            DrawingDisplayName = "Test.dwg"
        };
        var diagnostics = new List<OperationDiagnostic>
        {
            new()
            {
                Level = DiagnosticLevel.Warning,
                Code = "W1",
                Stage = PipelineStage.FloorConfigLoad,
                Module = "Test",
                Message = "warn"
            }
        };
        var document = new LoadedSectionConfig
        {
            Config = new SectionConfig
            {
                AlignmentBaseFloorName = "OLD",
                Floors =
                [
                    CreateFloor("OLD-1", 1000),
                    CreateFloor("OLD-2", 2000)
                ]
            },
            OutputConfig = outputConfig,
            RuntimeState = runtimeState,
            RuntimeDiagnostics = diagnostics
        };
        var request = new SaveFloorConfigRequestDto
        {
            AlignmentBaseFloorName = "F2",
            GlobalSlopeEnabled = true,
            GlobalSlopePercent = 2.0,
            GlobalSlopeTarget = "Structural",
            GlobalTopSlopeEnabled = true,
            GlobalTopSlopePercent = 1.5,
            GlobalTopSlopeTarget = "TopTarget",
            GlobalBottomSlopeEnabled = true,
            GlobalBottomSlopePercent = 0.5,
            GlobalBottomSlopeTarget = "BottomTarget",
            Floors =
            [
                new FloorConfigEditDto
                {
                    Name = "F2",
                    Height = 6200,
                    FinishThickness = 180,
                    BottomSlabThickness = 900,
                    TopSlabThickness = 700,
                    LegacySlopeEnabled = true,
                    LegacySlopePercent = 3.0,
                    LegacySlopeTarget = "LegacyTarget",
                    AlignmentPoints =
                    [
                        new Point3DDto { X = 10, Y = 20, Z = 30 },
                        new Point3DDto { X = 40, Y = 50, Z = 60 },
                        new Point3DDto { X = 70, Y = 80, Z = 90 }
                    ],
                    ScopeBounds = new ScopeBoundsDto
                    {
                        MinX = 1,
                        MinY = 2,
                        MaxX = 3,
                        MaxY = 4
                    },
                    TopBoundarySlab = new BoundarySlabEditDto
                    {
                        TemplateId = "TOP-2",
                        SlopeEnabled = true,
                        SlopePercent = 2.5,
                        SlopeTarget = "TopSurface"
                    },
                    BottomBoundarySlab = new BoundarySlabEditDto
                    {
                        TemplateId = "BOT-2",
                        SlopeEnabled = true,
                        SlopePercent = 1.0,
                        SlopeTarget = "BottomSurface"
                    }
                },
                new FloorConfigEditDto
                {
                    Name = "F1",
                    Height = 5200,
                    ScopeBounds = null,
                    TopBoundarySlab = new BoundarySlabEditDto
                    {
                        TemplateId = "TOP-1"
                    },
                    BottomBoundarySlab = new BoundarySlabEditDto
                    {
                        TemplateId = "BOT-1"
                    }
                }
            ]
        };

        _mapper.ApplyToDocument(request, document);

        document.Config.AlignmentBaseFloorName.Should().Be("F2");
        document.Config.GlobalSlopeEnabled.Should().BeTrue();
        document.Config.GlobalSlopeValue.Should().BeApproximately(0.02, 0.000001);
        document.Config.GlobalTopSlopeValue.Should().BeApproximately(0.015, 0.000001);
        document.Config.GlobalBottomSlopeValue.Should().BeApproximately(0.005, 0.000001);
        document.Config.Floors.Select(floor => floor.Name).Should().Equal("F2", "F1");

        var firstFloor = document.Config.Floors[0];
        firstFloor.Height.Should().Be(6200);
        firstFloor.HasSlope.Should().BeTrue();
        firstFloor.SlopeValue.Should().BeApproximately(0.03, 0.000001);
        firstFloor.AlignmentPoints.Should().HaveCount(3);
        firstFloor.AlignmentPoints[0].Should().Be(new Point3D(10, 20, 30));
        firstFloor.ScopeBounds.Should().Be(new ScopeBounds2D { MinX = 1, MinY = 2, MaxX = 3, MaxY = 4 });
        firstFloor.TopBoundarySlab.TemplateId.Should().Be("TOP-2");
        firstFloor.TopBoundarySlab.SlopeValue.Should().BeApproximately(0.025, 0.000001);
        firstFloor.BottomBoundarySlab.TemplateId.Should().Be("BOT-2");
        firstFloor.BottomBoundarySlab.SlopeValue.Should().BeApproximately(0.01, 0.000001);

        document.Config.Floors[1].ScopeBounds.Should().BeNull();
        document.OutputConfig.Should().BeSameAs(outputConfig);
        document.RuntimeState.Should().BeSameAs(runtimeState);
        document.RuntimeDiagnostics.Should().BeSameAs(diagnostics);
    }

    private static LoadedSectionConfig CreateDocument(SectionConfig config)
        => new()
        {
            Config = config,
            OutputConfig = new SectionOutputConfig(),
            RuntimeState = new SectionConfigRuntimeState
            {
                Source = SectionConfigStorageSource.EmbeddedDwg,
                DrawingDisplayName = "Test.dwg"
            },
            RuntimeDiagnostics = []
        };

    private static FloorConfig CreateFloor(string name, double height)
        => new()
        {
            Name = name,
            Height = height,
            TopBoundarySlab = new BoundarySlabConfig { TemplateId = $"TOP-{name}" },
            BottomBoundarySlab = new BoundarySlabConfig { TemplateId = $"BOT-{name}" }
        };
}
