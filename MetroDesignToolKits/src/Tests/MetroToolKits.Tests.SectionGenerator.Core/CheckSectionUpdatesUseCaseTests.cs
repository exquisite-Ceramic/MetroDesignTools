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
            .Returns(elements);

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
            .Returns(newElements);

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

    // ── 辅助 ──────────────────────────────────────────────────────────────────

    private static CheckSectionUpdatesUseCase BuildUseCase(
        ISectionSnapshotRepository? snapshotRepo = null,
        IElementRecognizer? recognizer = null,
        IFloorConfigRepository? configRepo = null)
    {
        snapshotRepo ??= Substitute.For<ISectionSnapshotRepository>();
        recognizer   ??= Substitute.For<IElementRecognizer>();
        configRepo   ??= Substitute.For<IFloorConfigRepository>();

        if (configRepo.Load() == null!)
            configRepo.Load().Returns(new SectionConfig());

        return new CheckSectionUpdatesUseCase(
            snapshotRepo,
            recognizer,
            configRepo,
            new FloorGeometryHasher(),
            NullLogger<CheckSectionUpdatesUseCase>.Instance);
    }
}
