using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using MetroToolKits.Foundation.Building.Elements;
using MetroToolKits.Foundation.Core.Diagnostics;
using MetroToolKits.Foundation.Core.Geometry;
using MetroToolKits.Foundation.Core.Logging;
using MetroToolKits.SectionGenerator.App.Abstractions;
using MetroToolKits.SectionGenerator.App.Diagnostics;
using MetroToolKits.SectionGenerator.App.UseCases;
using MetroToolKits.SectionGenerator.Core.Sections;
using NSubstitute;

namespace MetroToolKits.Tests.SectionGenerator.Core;

public class GenerateSectionUseCaseTests
{
    [Fact]
    public void Execute_UsesResolvedAlignmentForEachParticipatingFloorRecognition()
    {
        var configRepo = Substitute.For<IFloorConfigRepository>();
        configRepo.Load().Returns(new SectionConfig
        {
            AlignmentBaseFloorName = "F1",
            Floors = new List<FloorConfig>
            {
                DefaultFloor(
                    "F1",
                    new Point3D(0, 0, 0),
                    new Point3D(10, 0, 0),
                    new Point3D(0, 10, 0)),
                DefaultFloor(
                    "F2",
                    new Point3D(100, 0, 0),
                    new Point3D(110, 0, 0),
                    new Point3D(100, 10, 0))
            }
        });

        var recognizer = Substitute.For<IElementRecognizer>();
        recognizer.RecognizeElements(Arg.Any<Line3D>(), Arg.Any<double>())
            .Returns(callInfo =>
            {
                var line = callInfo.Arg<Line3D>();
                if (line.Start.Equals(new Point3D(0, 0, 0)))
                    return RecognitionResult(MakeWallAtX(0, "F1W"));

                if (line.Start.Equals(new Point3D(100, 0, 0)))
                    return RecognitionResult(MakeWallAtX(100, "F2W"));

                return RecognitionResult();
            });

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

        var useCase = BuildUseCase(
            configRepo,
            recognizer,
            drawingService,
            snapshotRepo,
            userLogger);

        var result = useCase.Execute(CreateRequest());

        result.Status.Should().Be(OperationStatus.Success);
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
            Arg.Is<SectionSnapshot>(snapshot =>
                snapshot.SourceCutLineHandle == "10" &&
                snapshot.FloorSnapshots.Count == 2));
    }

    [Fact]
    public void Execute_BaseFloorMissing_ReturnsFailed()
    {
        var configRepo = Substitute.For<IFloorConfigRepository>();
        configRepo.Load().Returns(new SectionConfig
        {
            Floors = new List<FloorConfig>
            {
                DefaultFloor("F1"),
                DefaultFloor("F2")
            }
        });

        var useCase = BuildUseCase(configRepo: configRepo);
        var result = useCase.Execute(CreateRequest());

        result.Status.Should().Be(OperationStatus.Failed);
        result.Failure!.Code.Should().Be(SectionGenerationErrorCodes.AlignmentBaseFloorMissing);
    }

    [Fact]
    public void Execute_SomeFloorsMissingAlignment_ReturnsPartialSuccess()
    {
        var configRepo = Substitute.For<IFloorConfigRepository>();
        configRepo.Load().Returns(new SectionConfig
        {
            AlignmentBaseFloorName = "F1",
            Floors = new List<FloorConfig>
            {
                DefaultFloor(
                    "F1",
                    new Point3D(0, 0, 0),
                    new Point3D(10, 0, 0),
                    new Point3D(0, 10, 0)),
                DefaultFloor("F2")
            }
        });

        var recognizer = Substitute.For<IElementRecognizer>();
        recognizer.RecognizeElements(Arg.Any<Line3D>(), Arg.Any<double>())
            .Returns(RecognitionResult(MakeWallAtX(0, "F1W")));

        var useCase = BuildUseCase(configRepo: configRepo, recognizer: recognizer);
        var result = useCase.Execute(CreateRequest());

        result.Status.Should().Be(OperationStatus.PartialSuccess);
        result.Success.Should().BeTrue();
        result.FloorCount.Should().Be(1);
        result.Diagnostics.Should().Contain(d => d.Code == SectionGenerationErrorCodes.AlignmentPointsMissing);
    }

    [Fact]
    public void Execute_AllRecognizedFloorsEmpty_ReturnsFailedWithNoRecognizedElements()
    {
        var recognizer = Substitute.For<IElementRecognizer>();
        recognizer.RecognizeElements(Arg.Any<Line3D>(), Arg.Any<double>())
            .Returns(new ElementRecognitionResult
            {
                Diagnostics = new[]
                {
                    SectionGenerationDiagnosticFactory.UnsupportedEntityType(
                        "LayerBasedElementRecognizer",
                        "A1",
                        "MK_结构墙",
                        "Polyline",
                        "Line")
                }
            });

        var useCase = BuildUseCase(recognizer: recognizer);
        var result = useCase.Execute(CreateRequest());

        result.Status.Should().Be(OperationStatus.Failed);
        result.Failure!.Code.Should().Be(SectionGenerationErrorCodes.NoRecognizedElements);
    }

    [Fact]
    public void Execute_DrawingFailure_ReturnsFailedWithDrawFailed()
    {
        var drawingService = Substitute.For<IDrawingService>();
        drawingService.DrawMultiFloorSectionBlock(
                Arg.Any<MultiFloorSectionData>(),
                Arg.Any<Point3D>(),
                Arg.Any<IReadOnlyList<FloorConfig>>())
            .Returns(_ => throw new InfrastructureException(
                SectionGenerationFailures.DrawFailed("事务提交失败")));

        var useCase = BuildUseCase(
            recognizer: SingleWallRecognizer(),
            drawingService: drawingService);

        var result = useCase.Execute(CreateRequest());

        result.Status.Should().Be(OperationStatus.Failed);
        result.Failure!.Code.Should().Be(SectionGenerationErrorCodes.DrawFailed);
    }

    [Fact]
    public void Execute_SnapshotSaveFailure_ReturnsFailedWithSnapshotSaveFailed()
    {
        var snapshotRepo = Substitute.For<ISectionSnapshotRepository>();
        snapshotRepo
            .When(repo => repo.Save(Arg.Any<string>(), Arg.Any<SectionSnapshot>()))
            .Do(_ => throw new InfrastructureException(
                SectionGenerationFailures.SnapshotSaveFailed("写入 XData 失败")));

        var useCase = BuildUseCase(
            recognizer: SingleWallRecognizer(),
            snapshotRepo: snapshotRepo);

        var result = useCase.Execute(CreateRequest());

        result.Status.Should().Be(OperationStatus.Failed);
        result.Failure!.Code.Should().Be(SectionGenerationErrorCodes.SnapshotSaveFailed);
    }

    private static GenerateSectionUseCase BuildUseCase(
        IFloorConfigRepository? configRepo = null,
        IElementRecognizer? recognizer = null,
        IDrawingService? drawingService = null,
        ISectionSnapshotRepository? snapshotRepo = null,
        IUserLogger? userLogger = null)
    {
        if (configRepo == null)
        {
            configRepo = Substitute.For<IFloorConfigRepository>();
            configRepo.Load().Returns(new SectionConfig
            {
                AlignmentBaseFloorName = "F1",
                Floors = new List<FloorConfig>
                {
                    DefaultFloor(
                        "F1",
                        new Point3D(0, 0, 0),
                        new Point3D(10, 0, 0),
                        new Point3D(0, 10, 0))
                }
            });
        }

        recognizer ??= RecognitionSubstitute(RecognitionResult(MakeWallAtX(0)));

        if (drawingService == null)
        {
            drawingService = Substitute.For<IDrawingService>();
            drawingService.DrawMultiFloorSectionBlock(
                    Arg.Any<MultiFloorSectionData>(),
                    Arg.Any<Point3D>(),
                    Arg.Any<IReadOnlyList<FloorConfig>>())
                .Returns(new DrawSectionBlockResult
                {
                    BlockName = "MK_剖面_F1",
                    BlockHandle = "ABCD"
                });
        }

        snapshotRepo ??= Substitute.For<ISectionSnapshotRepository>();
        userLogger ??= Substitute.For<IUserLogger>();

        return new GenerateSectionUseCase(
            configRepo,
            recognizer,
            new MultiFloorSectionComposer(new SectionComposer()),
            drawingService,
            snapshotRepo,
            new FloorGeometryHasher(),
            NullLogger<GenerateSectionUseCase>.Instance,
            userLogger);
    }

    private static GenerateSectionRequest CreateRequest() => new()
    {
        CutLineHandle = "10",
        CutLineStart = new Point3D(0, 0, 0),
        CutLineEnd = new Point3D(0, 20, 0),
        ViewDepth = 3000,
        InsertionPoint = new Point3D(5000, 0, 0)
    };

    private static FloorConfig DefaultFloor(string name = "F1", params Point3D[] alignmentPoints) => new()
    {
        Name = name,
        Height = 3000,
        BottomSlabThickness = 800,
        TopSlabThickness = 600,
        AlignmentPoints = alignmentPoints.ToList()
    };

    private static IElementRecognizer SingleWallRecognizer()
        => RecognitionSubstitute(RecognitionResult(MakeWallAtX(0)));

    private static IElementRecognizer RecognitionSubstitute(ElementRecognitionResult result)
    {
        var recognizer = Substitute.For<IElementRecognizer>();
        recognizer.RecognizeElements(Arg.Any<Line3D>(), Arg.Any<double>())
            .Returns(result);
        return recognizer;
    }

    private static ElementRecognitionResult RecognitionResult(params BuildingElement[] elements) => new()
    {
        Elements = elements,
        IntersectingElementCount = elements.Length,
        MatchedLayerCount = elements.Length,
        ScannedEntityCount = elements.Length
    };

    private static Wall MakeWallAtX(double x, string handle = "AAA") => new()
    {
        StartPoint = new Point3D(x - 500, 10, 0),
        EndPoint = new Point3D(x + 500, 10, 0),
        Height = 3000,
        Thickness = 200,
        SourceHandle = handle,
        SourceLayer = "MK_结构墙"
    };
}
