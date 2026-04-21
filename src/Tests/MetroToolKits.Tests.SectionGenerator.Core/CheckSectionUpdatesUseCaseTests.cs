using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using MetroToolKits.Foundation.Building.Elements;
using MetroToolKits.Foundation.Core.Diagnostics;
using MetroToolKits.Foundation.Core.Geometry;
using MetroToolKits.SectionGenerator.App.Abstractions;
using MetroToolKits.SectionGenerator.App.Diagnostics;
using MetroToolKits.SectionGenerator.App.UseCases;
using MetroToolKits.SectionGenerator.Core.Sections;
using NSubstitute;

namespace MetroToolKits.Tests.SectionGenerator.Core;

public class CheckSectionUpdatesUseCaseTests
{
    private static Wall MakeWall(string handle = "AAA") => new()
    {
        StartPoint = new Point3D(-500, 10, 0),
        EndPoint = new Point3D(500, 10, 0),
        Height = 3000,
        SourceHandle = handle
    };

    [Fact]
    public void Execute_NoBlocks_ReturnsSuccessWithEmptyItems()
    {
        var snapshotRepo = Substitute.For<ISectionSnapshotRepository>();
        snapshotRepo.FindAllSectionBlockHandles().Returns(Array.Empty<string>());

        var result = BuildUseCase(snapshotRepo).Execute();

        result.Status.Should().Be(OperationStatus.Success);
        result.Items.Should().BeEmpty();
    }

    [Fact]
    public void Execute_BlockWithNoSnapshot_ReturnsUnknownStatus()
    {
        var snapshotRepo = Substitute.For<ISectionSnapshotRepository>();
        snapshotRepo.FindAllSectionBlockHandles().Returns(new[] { "HANDLE1" });
        snapshotRepo.Load("HANDLE1").Returns((SectionSnapshot?)null);

        var result = BuildUseCase(snapshotRepo).Execute();

        result.Items.Should().HaveCount(1);
        result.Items[0].Status.Should().Be(SectionUpdateStatus.Unknown);
    }

    [Fact]
    public void Execute_BaseFloorMissing_ReturnsFailed()
    {
        var snapshotRepo = SnapshotRepoWithSingleSnapshot();
        var configRepo = Substitute.For<IFloorConfigRepository>();
        configRepo.Load().Returns(new SectionConfig
        {
            Floors = new List<FloorConfig>
            {
                DefaultFloor("F1"),
                DefaultFloor("F2")
            }
        });

        var result = BuildUseCase(snapshotRepo, configRepo: configRepo).Execute();

        result.Status.Should().Be(OperationStatus.Failed);
        result.Failure!.Code.Should().Be(SectionGenerationErrorCodes.AlignmentBaseFloorMissing);
        result.Items.Should().BeEmpty();
    }

    [Fact]
    public void Execute_HashUnchanged_ReturnsUpToDate()
    {
        var elements = new[] { MakeWall() };
        var hash = new FloorGeometryHasher().ComputeHash(elements);
        var snapshotRepo = SnapshotRepoWithSingleSnapshot(hash);

        var recognizer = Substitute.For<IElementRecognizer>();
        recognizer.RecognizeElements(Arg.Any<Line3D>(), Arg.Any<double>())
            .Returns(new ElementRecognitionResult { Elements = elements });

        var configRepo = Substitute.For<IFloorConfigRepository>();
        configRepo.Load().Returns(new SectionConfig
        {
            Floors = new List<FloorConfig> { DefaultFloor("F1") }
        });

        var result = BuildUseCase(snapshotRepo, recognizer, configRepo).Execute();

        result.Status.Should().Be(OperationStatus.Success);
        result.Items[0].Status.Should().Be(SectionUpdateStatus.UpToDate);
    }

    [Fact]
    public void Execute_HashChanged_ReturnsOutdated()
    {
        var oldHash = new FloorGeometryHasher().ComputeHash(Array.Empty<BuildingElement>());
        var snapshotRepo = SnapshotRepoWithSingleSnapshot(oldHash);

        var recognizer = Substitute.For<IElementRecognizer>();
        recognizer.RecognizeElements(Arg.Any<Line3D>(), Arg.Any<double>())
            .Returns(new ElementRecognitionResult { Elements = new[] { MakeWall() } });

        var configRepo = Substitute.For<IFloorConfigRepository>();
        configRepo.Load().Returns(new SectionConfig
        {
            Floors = new List<FloorConfig> { DefaultFloor("F1") }
        });

        var result = BuildUseCase(snapshotRepo, recognizer, configRepo).Execute();

        result.Items[0].Status.Should().Be(SectionUpdateStatus.Outdated);
        result.Items[0].OutdatedFloors.Should().Contain("F1");
    }

    [Fact]
    public void Execute_MissingNonBaseAlignment_ReturnsPartialSuccessAndSkippedFloor()
    {
        var hasher = new FloorGeometryHasher();
        var elements = new[] { MakeWall() };
        var snapshot = new SectionSnapshot
        {
            BlockName = "MK_剖面_F1_F2",
            SourceCutLineHandle = "71",
            CutLineStart = new Point3D(0, 0, 0),
            CutLineEnd = new Point3D(0, 20, 0),
            ViewDepth = 3000,
            FloorSnapshots = new List<FloorSnapshot>
            {
                new() { FloorName = "F1", GeometryHash = hasher.ComputeHash(elements), ElementCount = 1 },
                new() { FloorName = "F2", GeometryHash = hasher.ComputeHash(elements), ElementCount = 1 }
            }
        };

        var snapshotRepo = Substitute.For<ISectionSnapshotRepository>();
        snapshotRepo.FindAllSectionBlockHandles().Returns(new[] { "H1" });
        snapshotRepo.Load("H1").Returns(snapshot);

        var lineResolver = Substitute.For<ISectionLineResolver>();
        lineResolver.ResolveCurrentLine("71").Returns(new Line3D(new Point3D(0, 0, 0), new Point3D(0, 20, 0)));

        var recognizer = Substitute.For<IElementRecognizer>();
        recognizer.RecognizeElements(Arg.Any<Line3D>(), Arg.Any<double>())
            .Returns(new ElementRecognitionResult { Elements = elements });

        var configRepo = Substitute.For<IFloorConfigRepository>();
        configRepo.Load().Returns(new SectionConfig
        {
            AlignmentBaseFloorName = "F1",
            Floors = new List<FloorConfig>
            {
                DefaultFloor("F1",
                    new Point3D(0, 0, 0),
                    new Point3D(10, 0, 0),
                    new Point3D(0, 10, 0)),
                DefaultFloor("F2")
            }
        });

        var result = BuildUseCase(snapshotRepo, recognizer, configRepo, lineResolver).Execute();

        result.Status.Should().Be(OperationStatus.PartialSuccess);
        result.Diagnostics.Should().Contain(d => d.Code == SectionGenerationErrorCodes.AlignmentPointsMissing);
        result.Items[0].SkippedFloors.Should().Contain("F2");
        result.Items[0].Status.Should().Be(SectionUpdateStatus.Outdated);
    }

    [Fact]
    public void Execute_CurrentParticipatingFloorSetDiffersFromSnapshot_ReturnsOutdated()
    {
        var hasher = new FloorGeometryHasher();
        var elements = new[] { MakeWall() };
        var snapshot = new SectionSnapshot
        {
            BlockName = "MK_剖面_F1",
            SourceCutLineHandle = "71",
            CutLineStart = new Point3D(0, 0, 0),
            CutLineEnd = new Point3D(0, 20, 0),
            ViewDepth = 3000,
            FloorSnapshots = new List<FloorSnapshot>
            {
                new() { FloorName = "F1", GeometryHash = hasher.ComputeHash(elements), ElementCount = 1 }
            }
        };

        var snapshotRepo = Substitute.For<ISectionSnapshotRepository>();
        snapshotRepo.FindAllSectionBlockHandles().Returns(new[] { "H1" });
        snapshotRepo.Load("H1").Returns(snapshot);

        var lineResolver = Substitute.For<ISectionLineResolver>();
        lineResolver.ResolveCurrentLine("71").Returns(new Line3D(new Point3D(0, 0, 0), new Point3D(0, 20, 0)));

        var recognizer = Substitute.For<IElementRecognizer>();
        recognizer.RecognizeElements(Arg.Any<Line3D>(), Arg.Any<double>())
            .Returns(new ElementRecognitionResult { Elements = elements });

        var configRepo = Substitute.For<IFloorConfigRepository>();
        configRepo.Load().Returns(new SectionConfig
        {
            AlignmentBaseFloorName = "F1",
            Floors = new List<FloorConfig>
            {
                DefaultFloor("F1",
                    new Point3D(0, 0, 0),
                    new Point3D(10, 0, 0),
                    new Point3D(0, 10, 0)),
                DefaultFloor("F2",
                    new Point3D(100, 0, 0),
                    new Point3D(110, 0, 0),
                    new Point3D(100, 10, 0))
            }
        });

        var result = BuildUseCase(snapshotRepo, recognizer, configRepo, lineResolver).Execute();

        result.Items[0].Status.Should().Be(SectionUpdateStatus.Outdated);
        result.Items[0].OutdatedFloors.Should().Contain("F2");
    }

    private static CheckSectionUpdatesUseCase BuildUseCase(
        ISectionSnapshotRepository? snapshotRepo = null,
        IElementRecognizer? recognizer = null,
        IFloorConfigRepository? configRepo = null,
        ISectionLineResolver? lineResolver = null)
    {
        snapshotRepo ??= Substitute.For<ISectionSnapshotRepository>();
        recognizer ??= Substitute.For<IElementRecognizer>();
        lineResolver ??= Substitute.For<ISectionLineResolver>();

        if (configRepo == null)
        {
            configRepo = Substitute.For<IFloorConfigRepository>();
            configRepo.Load().Returns(new SectionConfig
            {
                Floors = new List<FloorConfig> { DefaultFloor("F1") }
            });
        }

        return new CheckSectionUpdatesUseCase(
            snapshotRepo,
            lineResolver,
            recognizer,
            configRepo,
            new FloorGeometryHasher(),
            NullLogger<CheckSectionUpdatesUseCase>.Instance);
    }

    private static ISectionSnapshotRepository SnapshotRepoWithSingleSnapshot(string? geometryHash = null)
    {
        var repo = Substitute.For<ISectionSnapshotRepository>();
        repo.FindAllSectionBlockHandles().Returns(new[] { "H1" });
        repo.Load("H1").Returns(new SectionSnapshot
        {
            BlockName = "MK_剖面_F1",
            SourceCutLineHandle = "71",
            CutLineStart = new Point3D(0, 0, 0),
            CutLineEnd = new Point3D(0, 20, 0),
            ViewDepth = 3000,
            FloorSnapshots = new List<FloorSnapshot>
            {
                new() { FloorName = "F1", GeometryHash = geometryHash ?? string.Empty, ElementCount = 1 }
            }
        });
        return repo;
    }

    private static FloorConfig DefaultFloor(string name, params Point3D[] alignmentPoints) => new()
    {
        Name = name,
        Height = 3000,
        BottomSlabThickness = 800,
        TopSlabThickness = 600,
        AlignmentPoints = alignmentPoints.ToList()
    };
}
