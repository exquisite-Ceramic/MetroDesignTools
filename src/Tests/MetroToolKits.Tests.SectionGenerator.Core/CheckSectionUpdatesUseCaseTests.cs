using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using MetroToolKits.Foundation.Building.Elements;
using MetroToolKits.Foundation.Core.Geometry;
using MetroToolKits.SectionGenerator.App.Abstractions;
using MetroToolKits.SectionGenerator.App.UseCases;
using MetroToolKits.SectionGenerator.Core.Sections;
using NSubstitute;

namespace MetroToolKits.Tests.SectionGenerator.Core;

public class CheckSectionUpdatesUseCaseTests
{
    private static readonly Line3D DummyLine =
        new(new Point3D(0, 0, 0), new Point3D(1, 0, 0));

    private static Wall MakeWall(string handle = "AAA") => new()
    {
        StartPoint   = new Point3D(0, 0, 0),
        EndPoint     = new Point3D(0, 5000, 0),
        Height       = 3000,
        SourceHandle = handle
    };

    private static FloorConfig DefaultFloor(string name = "F1") => new()
    {
        Name = name, Height = 3000, BottomSlabThickness = 800, TopSlabThickness = 600
    };

    // ── 无剖面块 ──────────────────────────────────────────────────────────────

    [Fact]
    public void Execute_NoBlocks_ReturnsEmptyList()
    {
        var snapshotRepo = Substitute.For<ISectionSnapshotRepository>();
        snapshotRepo.FindAllSectionBlockHandles().Returns(Array.Empty<string>());

        var useCase = BuildUseCase(snapshotRepo);
        var results = useCase.Execute();

        results.Should().BeEmpty();
    }

    // ── 无快照的块 ────────────────────────────────────────────────────────────

    [Fact]
    public void Execute_BlockWithNoSnapshot_ReturnsUnknownStatus()
    {
        var snapshotRepo = Substitute.For<ISectionSnapshotRepository>();
        snapshotRepo.FindAllSectionBlockHandles().Returns(new[] { "HANDLE1" });
        snapshotRepo.Load("HANDLE1").Returns((SectionSnapshot?)null);

        var useCase = BuildUseCase(snapshotRepo);
        var results = useCase.Execute();

        results.Should().HaveCount(1);
        results[0].Status.Should().Be(SectionUpdateStatus.Unknown);
        results[0].BlockHandle.Should().Be("HANDLE1");
    }

    // ── 哈希未变化 → UpToDate ─────────────────────────────────────────────────

    [Fact]
    public void Execute_HashUnchanged_ReturnsUpToDate()
    {
        var hasher   = new FloorGeometryHasher();
        var elements = new[] { MakeWall() };
        var hash     = hasher.ComputeHash(elements);

        var snapshot = new SectionSnapshot
        {
            BlockName = "MK_剖面_F1",
            FloorSnapshots = new List<FloorSnapshot>
            {
                new() { FloorName = "F1", GeometryHash = hash, ElementCount = 1 }
            }
        };

        var snapshotRepo = Substitute.For<ISectionSnapshotRepository>();
        snapshotRepo.FindAllSectionBlockHandles().Returns(new[] { "HANDLE1" });
        snapshotRepo.Load("HANDLE1").Returns(snapshot);

        var recognizer = Substitute.For<IElementRecognizer>();
        recognizer.RecognizeElements(Arg.Any<Line3D>(), Arg.Any<double>())
            .Returns(new ElementRecognitionResult { Elements = elements });

        var configRepo = Substitute.For<IFloorConfigRepository>();
        configRepo.Load().Returns(new SectionConfig
        {
            Floors = new List<FloorConfig> { DefaultFloor("F1") }
        });

        var useCase = BuildUseCase(snapshotRepo, recognizer, configRepo);
        var results = useCase.Execute();

        results.Should().HaveCount(1);
        results[0].Status.Should().Be(SectionUpdateStatus.UpToDate);
        results[0].OutdatedFloors.Should().BeEmpty();
    }

    // ── 哈希变化 → Outdated ───────────────────────────────────────────────────

    [Fact]
    public void Execute_HashChanged_ReturnsOutdated()
    {
        // 快照中存的是旧哈希（空构件）
        var hasher      = new FloorGeometryHasher();
        var oldHash     = hasher.ComputeHash(Enumerable.Empty<BuildingElement>());
        var newElements = new[] { MakeWall() }; // 现在有构件了

        var snapshot = new SectionSnapshot
        {
            BlockName = "MK_剖面_F1",
            FloorSnapshots = new List<FloorSnapshot>
            {
                new() { FloorName = "F1", GeometryHash = oldHash, ElementCount = 0 }
            }
        };

        var snapshotRepo = Substitute.For<ISectionSnapshotRepository>();
        snapshotRepo.FindAllSectionBlockHandles().Returns(new[] { "HANDLE1" });
        snapshotRepo.Load("HANDLE1").Returns(snapshot);

        var recognizer = Substitute.For<IElementRecognizer>();
        recognizer.RecognizeElements(Arg.Any<Line3D>(), Arg.Any<double>())
            .Returns(new ElementRecognitionResult { Elements = newElements });

        var configRepo = Substitute.For<IFloorConfigRepository>();
        configRepo.Load().Returns(new SectionConfig
        {
            Floors = new List<FloorConfig> { DefaultFloor("F1") }
        });

        var useCase = BuildUseCase(snapshotRepo, recognizer, configRepo);
        var results = useCase.Execute();

        results.Should().HaveCount(1);
        results[0].Status.Should().Be(SectionUpdateStatus.Outdated);
        results[0].OutdatedFloors.Should().Contain("F1");
    }

    // ── 多个剖面块 ────────────────────────────────────────────────────────────

    [Fact]
    public void Execute_MultipleBlocks_ReturnsResultForEach()
    {
        var snapshotRepo = Substitute.For<ISectionSnapshotRepository>();
        snapshotRepo.FindAllSectionBlockHandles().Returns(new[] { "H1", "H2", "H3" });
        snapshotRepo.Load(Arg.Any<string>()).Returns((SectionSnapshot?)null);

        var useCase = BuildUseCase(snapshotRepo);
        var results = useCase.Execute();

        results.Should().HaveCount(3);
    }

    // ── 楼层不在配置中 ────────────────────────────────────────────────────────

    [Fact]
    public void Execute_FloorNotInConfig_SkipsFloor()
    {
        var hasher = new FloorGeometryHasher();
        var snapshot = new SectionSnapshot
        {
            BlockName = "MK_剖面_F99",
            FloorSnapshots = new List<FloorSnapshot>
            {
                new() { FloorName = "F99", GeometryHash = "oldhash", ElementCount = 0 }
            }
        };

        var snapshotRepo = Substitute.For<ISectionSnapshotRepository>();
        snapshotRepo.FindAllSectionBlockHandles().Returns(new[] { "H1" });
        snapshotRepo.Load("H1").Returns(snapshot);

        var configRepo = Substitute.For<IFloorConfigRepository>();
        configRepo.Load().Returns(new SectionConfig
        {
            Floors = new List<FloorConfig> { DefaultFloor("F1") } // 没有 F99
        });

        var useCase = BuildUseCase(snapshotRepo, configRepo: configRepo);
        var results = useCase.Execute();

        // F99 不在配置中，跳过比对，结果为 UpToDate（无 outdated floors）
        results[0].OutdatedFloors.Should().BeEmpty();
    }

    [Fact]
    public void Execute_UsesAlignedSectionLineWhenCheckingConfiguredFloor()
    {
        var snapshot = new SectionSnapshot
        {
            BlockName = "MK_剖面_F2",
            CutLineStart = new Point3D(0, 0, 0),
            CutLineEnd = new Point3D(0, 20, 0),
            ViewDepth = 3000,
            FloorSnapshots = new List<FloorSnapshot>
            {
                new() { FloorName = "F2", GeometryHash = "hash", ElementCount = 0 }
            }
        };

        var snapshotRepo = Substitute.For<ISectionSnapshotRepository>();
        snapshotRepo.FindAllSectionBlockHandles().Returns(new[] { "H1" });
        snapshotRepo.Load("H1").Returns(snapshot);

        var recognizer = Substitute.For<IElementRecognizer>();
        recognizer.RecognizeElements(Arg.Any<Line3D>(), Arg.Any<double>())
            .Returns(new ElementRecognitionResult());

        var configRepo = Substitute.For<IFloorConfigRepository>();
        configRepo.Load().Returns(new SectionConfig
        {
            Floors = new List<FloorConfig>
            {
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

        var useCase = BuildUseCase(snapshotRepo, recognizer, configRepo);
        _ = useCase.Execute();

        recognizer.Received(1).RecognizeElements(
            Arg.Is<Line3D>(line =>
                line.Start.Equals(new Point3D(100, 0, 0)) &&
                line.End.Equals(new Point3D(100, 20, 0))),
            3000);
    }

    [Fact]
    public void Execute_PrefersResolvedCurrentSourceLine_WhenCutLineHandleCanBeResolved()
    {
        var expectedLine = new Line3D(new Point3D(50, 0, 0), new Point3D(50, 20, 0));
        var elements = new[] { MakeWall() };
        var hash = new FloorGeometryHasher().ComputeHash(elements);

        var snapshot = new SectionSnapshot
        {
            BlockName = "MK_剖面_F1",
            SourceCutLineHandle = "71",
            CutLineStart = new Point3D(0, 0, 0),
            CutLineEnd = new Point3D(0, 20, 0),
            ViewDepth = 3000,
            FloorSnapshots = new List<FloorSnapshot>
            {
                new() { FloorName = "F1", GeometryHash = hash, ElementCount = 1 }
            }
        };

        var snapshotRepo = Substitute.For<ISectionSnapshotRepository>();
        snapshotRepo.FindAllSectionBlockHandles().Returns(new[] { "H1" });
        snapshotRepo.Load("H1").Returns(snapshot);

        var lineResolver = Substitute.For<ISectionLineResolver>();
        lineResolver.ResolveCurrentLine("71").Returns(expectedLine);

        var recognizer = Substitute.For<IElementRecognizer>();
        recognizer.RecognizeElements(
                Arg.Is<Line3D>(line => line.Start.Equals(expectedLine.Start) && line.End.Equals(expectedLine.End)),
                3000)
            .Returns(new ElementRecognitionResult { Elements = elements });

        var configRepo = Substitute.For<IFloorConfigRepository>();
        configRepo.Load().Returns(new SectionConfig
        {
            Floors = new List<FloorConfig> { DefaultFloor("F1") }
        });

        var useCase = BuildUseCase(snapshotRepo, recognizer, configRepo, lineResolver);
        var results = useCase.Execute();

        results.Should().HaveCount(1);
        results[0].Status.Should().Be(SectionUpdateStatus.UpToDate);
        recognizer.Received(1).RecognizeElements(
            Arg.Is<Line3D>(line => line.Start.Equals(expectedLine.Start) && line.End.Equals(expectedLine.End)),
            3000);
    }

    // ── 辅助 ──────────────────────────────────────────────────────────────────

    private static CheckSectionUpdatesUseCase BuildUseCase(
        ISectionSnapshotRepository? snapshotRepo = null,
        IElementRecognizer? recognizer = null,
        IFloorConfigRepository? configRepo = null,
        ISectionLineResolver? lineResolver = null)
    {
        snapshotRepo ??= Substitute.For<ISectionSnapshotRepository>();
        recognizer   ??= Substitute.For<IElementRecognizer>();
        configRepo   ??= Substitute.For<IFloorConfigRepository>();
        lineResolver ??= Substitute.For<ISectionLineResolver>();

        if (configRepo.Load() == null!)
            configRepo.Load().Returns(new SectionConfig());

        return new CheckSectionUpdatesUseCase(
            snapshotRepo,
            lineResolver,
            recognizer,
            configRepo,
            new FloorGeometryHasher(),
            NullLogger<CheckSectionUpdatesUseCase>.Instance);
    }
}
