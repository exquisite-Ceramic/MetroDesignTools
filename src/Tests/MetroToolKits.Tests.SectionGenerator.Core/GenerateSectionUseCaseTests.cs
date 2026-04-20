using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using MetroToolKits.Foundation.Building.Elements;
using MetroToolKits.Foundation.Core.Geometry;
using MetroToolKits.Foundation.Core.Logging;
using MetroToolKits.SectionGenerator.App.Abstractions;
using MetroToolKits.SectionGenerator.App.UseCases;
using MetroToolKits.SectionGenerator.Core.Sections;
using NSubstitute;

namespace MetroToolKits.Tests.SectionGenerator.Core;

public class GenerateSectionUseCaseTests
{
    [Fact]
    public void Execute_UsesAlignedSectionLineForEachFloorRecognition()
    {
        var configRepo = Substitute.For<IFloorConfigRepository>();
        configRepo.Load().Returns(new SectionConfig
        {
            Floors = new List<FloorConfig>
            {
                new()
                {
                    Name = "F1",
                    Height = 3000,
                    BottomSlabThickness = 800,
                    TopSlabThickness = 600
                },
                new()
                {
                    Name = "F2",
                    Height = 3000,
                    BottomSlabThickness = 800,
                    TopSlabThickness = 600,
                    AlignmentSourcePoints =
                    {
                        new Point3D(0, 0, 0),
                        new Point3D(10, 0, 0),
                        new Point3D(0, 10, 0)
                    },
                    AlignmentTargetPoints =
                    {
                        new Point3D(100, 0, 0),
                        new Point3D(110, 0, 0),
                        new Point3D(100, 10, 0)
                    }
                }
            }
        });

        var recognizer = Substitute.For<IElementRecognizer>();
        recognizer.RecognizeElements(Arg.Any<Line3D>(), Arg.Any<double>())
            .Returns(Array.Empty<BuildingElement>());

        var drawingService = Substitute.For<IDrawingService>();
        drawingService.DrawMultiFloorSectionBlock(
                Arg.Any<MultiFloorSectionData>(),
                Arg.Any<Point3D>(),
                Arg.Any<IReadOnlyList<FloorConfig>>())
            .Returns(new DrawSectionBlockResult
            {
                BlockName = "MK_剖面_F1_F2",
                BlockHandle = "ABCD"
            });

        var snapshotRepo = Substitute.For<ISectionSnapshotRepository>();
        var userLogger = Substitute.For<IUserLogger>();

        var useCase = new GenerateSectionUseCase(
            configRepo,
            recognizer,
            new MultiFloorSectionComposer(new SectionComposer()),
            drawingService,
            snapshotRepo,
            new FloorGeometryHasher(),
            NullLogger<GenerateSectionUseCase>.Instance,
            userLogger);

        var request = new GenerateSectionRequest
        {
            CutLineHandle = "10",
            CutLineStart = new Point3D(0, 0, 0),
            CutLineEnd = new Point3D(0, 20, 0),
            ViewDepth = 3000,
            InsertionPoint = new Point3D(5000, 0, 0)
        };

        var result = useCase.Execute(request);

        result.Success.Should().BeTrue();
        recognizer.Received(1).RecognizeElements(
            Arg.Is<Line3D>(line =>
                line.Start.Equals(new Point3D(0, 0, 0)) &&
                line.End.Equals(new Point3D(0, 20, 0))),
            3000);
        recognizer.Received(1).RecognizeElements(
            Arg.Is<Line3D>(line =>
                line.Start.Equals(new Point3D(100, 0, 0)) &&
                line.End.Equals(new Point3D(100, 20, 0))),
            3000);
        snapshotRepo.Received(1).Save(
            "ABCD",
            Arg.Is<SectionSnapshot>(snapshot => snapshot.SourceCutLineHandle == "10"));
    }
}
