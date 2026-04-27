using FluentAssertions;
using MetroToolKits.Foundation.Building.Types;
using MetroToolKits.Foundation.Core.Geometry;
using MetroToolKits.SectionGenerator.App.Abstractions;
using MetroToolKits.SectionGenerator.App.Models;
using MetroToolKits.SectionGenerator.App.Support;
using MetroToolKits.SectionGenerator.App.ViewModels;
using MetroToolKits.SectionGenerator.Core.Sections;

namespace MetroToolKits.Tests.SectionGenerator.Core;

public class FloorConfigViewModelTests
{
    private readonly IFloorConfigSaveRequestMapper _floorMapper = new FloorConfigSaveRequestMapper();
    private readonly ISectionOutputConfigMapper _outputMapper = new SectionOutputConfigMapper();

    [Fact]
    public void AddFloor_AppendsDefaultFloorAndSelectsIt()
    {
        var viewModel = CreateViewModel(CreateDocument());

        var floor = viewModel.AddFloor();

        viewModel.Floors.Should().ContainSingle();
        viewModel.SelectedFloor.Should().BeSameAs(floor);
        floor.Name.Should().Be("F1");
        floor.Height.Should().Be(3000);
        viewModel.AlignmentBaseFloorName.Should().Be("F1");
    }

    [Fact]
    public void DeleteSelectedFloor_RemovesFloorAndSelectsNextAvailableFloor()
    {
        var viewModel = CreateViewModel(CreateDocument(CreateFloor("F1"), CreateFloor("F2"), CreateFloor("F3")), "F2");

        viewModel.DeleteSelectedFloor();

        viewModel.Floors.Select(floor => floor.Name).Should().Equal("F1", "F3");
        viewModel.SelectedFloor!.Name.Should().Be("F3");
    }

    [Fact]
    public void DeleteSelectedFloor_WhenRemovingLastFloor_FallsBackToPreviousFloor()
    {
        var viewModel = CreateViewModel(CreateDocument(CreateFloor("F1"), CreateFloor("F2"), CreateFloor("F3")), "F3");

        viewModel.DeleteSelectedFloor();

        viewModel.Floors.Select(floor => floor.Name).Should().Equal("F1", "F2");
        viewModel.SelectedFloor!.Name.Should().Be("F2");
    }

    [Fact]
    public void MoveSelectedFloor_ReordersFloors()
    {
        var viewModel = CreateViewModel(CreateDocument(CreateFloor("F1"), CreateFloor("F2"), CreateFloor("F3")), "F2");

        viewModel.MoveSelectedFloorUp().Should().BeTrue();
        viewModel.Floors.Select(floor => floor.Name).Should().Equal("F2", "F1", "F3");

        viewModel.MoveSelectedFloorDown().Should().BeTrue();
        viewModel.Floors.Select(floor => floor.Name).Should().Equal("F1", "F2", "F3");
    }

    [Fact]
    public void MoveSelectedFloor_KeepsSelectedFloor()
    {
        var viewModel = CreateViewModel(CreateDocument(CreateFloor("F1"), CreateFloor("F2"), CreateFloor("F3")), "F2");
        var selectedFloor = viewModel.SelectedFloor;

        viewModel.MoveSelectedFloorUp().Should().BeTrue();
        viewModel.SelectedFloor.Should().BeSameAs(selectedFloor);
        viewModel.SelectedFloor!.Name.Should().Be("F2");

        viewModel.MoveSelectedFloorDown().Should().BeTrue();
        viewModel.SelectedFloor.Should().BeSameAs(selectedFloor);
        viewModel.SelectedFloor!.Name.Should().Be("F2");
    }

    [Fact]
    public void SelectFloor_PreservesPreviousFloorEditsForExport()
    {
        var viewModel = CreateViewModel(CreateDocument(CreateFloor("F1"), CreateFloor("F2")), "F1");
        viewModel.SelectedFloor!.Name = "F1-Edited";
        viewModel.SelectedFloor.Height = 4200;
        viewModel.SelectedFloor.TopBoundarySlopeEnabled = true;
        viewModel.SelectedFloor.TopBoundarySlopePercent = 2.5;

        viewModel.SelectFloor("F2");
        var request = viewModel.BuildSaveRequest(_floorMapper, _outputMapper);

        request.FloorConfig.Floors.Select(floor => floor.Name).Should().Equal("F1-Edited", "F2");
        request.FloorConfig.Floors[0].Height.Should().Be(4200);
        request.FloorConfig.Floors[0].TopBoundarySlab.SlopeEnabled.Should().BeTrue();
        request.FloorConfig.Floors[0].TopBoundarySlab.SlopePercent.Should().Be(2.5);
    }

    [Fact]
    public void BuildSaveRequest_PreservesTemplateIdsAlignmentPointsAndScopeBounds()
    {
        var document = CreateDocument(CreateFloor("F1"), CreateFloor("F2"));
        document.Config.Floors[0].TopBoundarySlab.TemplateId = "TOP-CUSTOM";
        document.Config.Floors[0].BottomBoundarySlab.TemplateId = "BOTTOM-CUSTOM";
        document.Config.Floors[0].AlignmentPoints =
        [
            new Point3D(10, 20, 30),
            new Point3D(40, 50, 60),
            new Point3D(70, 80, 90)
        ];
        document.Config.Floors[0].ScopeBounds = new ScopeBounds2D
        {
            MinX = 1,
            MinY = 2,
            MaxX = 300,
            MaxY = 400
        };
        var viewModel = CreateViewModel(document, "F1");

        var request = viewModel.BuildSaveRequest(_floorMapper, _outputMapper);

        request.FloorConfig.Floors[0].TopBoundarySlab.TemplateId.Should().Be("TOP-CUSTOM");
        request.FloorConfig.Floors[0].BottomBoundarySlab.TemplateId.Should().Be("BOTTOM-CUSTOM");
        request.FloorConfig.Floors[0].AlignmentPoints.Select(point => (point.X, point.Y, point.Z))
            .Should().Equal((10d, 20d, 30d), (40d, 50d, 60d), (70d, 80d, 90d));
        request.FloorConfig.Floors[0].ScopeBounds.Should().NotBeNull();
        request.FloorConfig.Floors[0].ScopeBounds!.MaxX.Should().Be(300);
        request.FloorConfig.Floors[0].ScopeBounds!.MaxY.Should().Be(400);
    }

    [Fact]
    public void BuildSaveRequest_ExportsFloorConfigDocumentRequest()
    {
        var document = CreateDocument(CreateFloor("F1"), CreateFloor("F2"));
        document.Config.AlignmentBaseFloorName = "F1";
        document.Config.GlobalSlopeEnabled = true;
        document.Config.GlobalSlopeValue = 0.015;
        document.Config.GlobalSlopeTarget = "FinishLayer";
        document.Config.Floors[0].AlignmentPoints =
        [
            new Point3D(0, 0, 0),
            new Point3D(1, 0, 0),
            new Point3D(0, 1, 0)
        ];
        document.Config.Floors[0].ScopeBounds = new ScopeBounds2D
        {
            MinX = 0,
            MinY = 0,
            MaxX = 100,
            MaxY = 200
        };
        var viewModel = CreateViewModel(document, "F1");

        var request = viewModel.BuildSaveRequest(_floorMapper, _outputMapper);

        request.FloorConfig.AlignmentBaseFloorName.Should().Be("F1");
        request.FloorConfig.GlobalSlopeEnabled.Should().BeTrue();
        request.FloorConfig.GlobalSlopePercent.Should().Be(1.5);
        request.FloorConfig.GlobalSlopeTarget.Should().Be("FinishLayer");
        request.FloorConfig.Floors.Should().HaveCount(2);
        request.FloorConfig.Floors[0].AlignmentPoints.Should().HaveCount(3);
        request.FloorConfig.Floors[0].ScopeBounds!.MaxY.Should().Be(200);
    }

    [Fact]
    public void ClearSelectedFloorState_ExportsEmptyAlignmentAndScope()
    {
        var document = CreateDocument(CreateFloor("F1"));
        document.Config.Floors[0].AlignmentPoints =
        [
            new Point3D(1, 2, 3),
            new Point3D(4, 5, 6),
            new Point3D(7, 8, 9)
        ];
        document.Config.Floors[0].ScopeBounds = new ScopeBounds2D
        {
            MinX = 0,
            MinY = 0,
            MaxX = 10,
            MaxY = 20
        };
        var viewModel = CreateViewModel(document, "F1");

        viewModel.ClearSelectedFloorAlignment();
        viewModel.ClearSelectedFloorScope();
        var request = viewModel.BuildSaveRequest(_floorMapper, _outputMapper);

        request.FloorConfig.Floors[0].AlignmentPoints.Should().BeEmpty();
        request.FloorConfig.Floors[0].ScopeBounds.Should().BeNull();
    }

    [Fact]
    public void BuildSaveRequest_ExportsOutputConfig()
    {
        var viewModel = CreateViewModel(CreateDocument(CreateFloor("F1")));
        viewModel.OutputConfig.GenerateAnnotations = true;
        viewModel.OutputConfig.HatchEnabled = true;
        viewModel.OutputConfig.WallHatch.PatternName = "SOLID";
        viewModel.OutputConfig.WallHatch.Scale = 25;
        viewModel.OutputConfig.WallHatch.Angle = 30;
        viewModel.OutputConfig.WallHatch.UseByLayer = false;
        viewModel.OutputConfig.CutLineLayer = " CUT ";
        viewModel.OutputConfig.SightLineLayer = "SIGHT";
        viewModel.OutputConfig.AnnotationLayer = "ANNO";
        viewModel.OutputConfig.WallHatchLayer = "WALL-H";
        viewModel.OutputConfig.ColumnHatchLayer = "COL-H";
        viewModel.OutputConfig.SlabHatchLayer = "SLAB-H";
        viewModel.OutputConfig.StructuralLayer = "STRU";
        viewModel.OutputConfig.FinishLayer = "FIN";

        var request = viewModel.BuildSaveRequest(_floorMapper, _outputMapper);

        request.OutputConfig.AnnotationOptions!.GenerateAnnotations.Should().BeTrue();
        request.OutputConfig.HatchOptions!.Enabled.Should().BeTrue();
        request.OutputConfig.HatchOptions.WallHatch!.PatternName.Should().Be("SOLID");
        request.OutputConfig.HatchOptions.WallHatch.Scale.Should().Be(25);
        request.OutputConfig.HatchOptions.WallHatch.Angle.Should().Be(30);
        request.OutputConfig.HatchOptions.WallHatch.UseByLayer.Should().BeFalse();
        request.OutputConfig.LayerOptions!.CutLineLayer.Should().Be("CUT");
        request.OutputConfig.LayerOptions.FinishLayer.Should().Be("FIN");
    }

    private FloorConfigViewModel CreateViewModel(LoadedSectionConfig document, string? selectedFloorName = null)
    {
        var viewModel = new FloorConfigViewModel();
        viewModel.Load(document, _outputMapper, selectedFloorName);
        return viewModel;
    }

    private static LoadedSectionConfig CreateDocument(params FloorConfig[] floors)
        => new()
        {
            Config = new SectionConfig
            {
                Floors = floors.ToList()
            },
            OutputConfig = new SectionOutputConfig(),
            RuntimeDiagnostics = [],
            RuntimeState = new SectionConfigRuntimeState()
        };

    private static FloorConfig CreateFloor(string name)
        => new()
        {
            Name = name,
            Height = 3000,
            TopBoundarySlab = new BoundarySlabConfig
            {
                TemplateId = $"TOP-{name}",
                SlopeTarget = "StructuralSlab"
            },
            BottomBoundarySlab = new BoundarySlabConfig
            {
                TemplateId = $"BOTTOM-{name}",
                SlopeTarget = "StructuralSlab"
            }
        };
}
