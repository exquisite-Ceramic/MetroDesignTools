using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using MetroToolKits.Foundation.Building.Elements;
using MetroToolKits.Foundation.Building.Types;
using MetroToolKits.Foundation.Core.Diagnostics;
using MetroToolKits.Foundation.Core.Geometry;
using MetroToolKits.SectionGenerator.App.Abstractions;
using MetroToolKits.SectionGenerator.App.Diagnostics;
using MetroToolKits.SectionGenerator.App.Models;
using MetroToolKits.SectionGenerator.App.Support;
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
        var blockQueryService = Substitute.For<ISectionBlockQueryService>();
        blockQueryService.FindAllSectionBlockHandles().Returns(Array.Empty<string>());

        var result = BuildUseCase(snapshotRepo, blockQueryService: blockQueryService).Execute();

        result.Status.Should().Be(OperationStatus.Success);
        result.Items.Should().BeEmpty();
    }

    [Fact]
    public void Execute_BlockWithNoSnapshot_ReturnsUnknownStatus()
    {
        var snapshotRepo = Substitute.For<ISectionSnapshotRepository>();
        var blockQueryService = Substitute.For<ISectionBlockQueryService>();
        blockQueryService.FindAllSectionBlockHandles().Returns(new[] { "HANDLE1" });
        snapshotRepo.Load("HANDLE1").Returns((SectionSnapshot?)null);

        var result = BuildUseCase(snapshotRepo, blockQueryService: blockQueryService).Execute();

        result.Items.Should().HaveCount(1);
        result.Items[0].Status.Should().Be(SectionUpdateStatus.Unknown);
    }

    [Fact]
    public void Execute_WhenEmbeddedConfigMissing_ReturnsUnknownForAllSections()
    {
        var snapshotRepo = SnapshotRepoWithSingleSnapshot("old-hash");
        var blockQueryService = Substitute.For<ISectionBlockQueryService>();
        blockQueryService.FindAllSectionBlockHandles().Returns(new[] { "H1" });
        var recognizer = Substitute.For<IElementRecognizer>();
        var configRepo = Substitute.For<IFloorConfigRepository>();
        configRepo.Load().Returns(new LoadedSectionConfig
        {
            Config = new SectionConfig(),
            OutputConfig = new SectionOutputConfig(),
            RuntimeState = new SectionConfigRuntimeState
            {
                Source = SectionConfigStorageSource.Missing,
                DrawingDisplayName = "44.dwg"
            }
        });

        var result = BuildUseCase(
            snapshotRepo,
            recognizer,
            configRepo,
            blockQueryService: blockQueryService).Execute();

        result.Status.Should().Be(OperationStatus.PartialSuccess);
        result.Items.Should().HaveCount(1);
        result.Items[0].Status.Should().Be(SectionUpdateStatus.Unknown);
        result.Items[0].WarningMessage.Should().Contain("缺少内嵌楼层配置");
        result.Diagnostics.Should().Contain(d =>
            d.Code == SectionGenerationErrorCodes.FloorConfigMissing &&
            d.Module == nameof(CheckSectionUpdatesUseCase));
        recognizer.DidNotReceive().RecognizeElements(Arg.Any<Line3D>(), Arg.Any<double>(), Arg.Any<ScopeBounds2D?>());
    }

    [Fact]
    public void Execute_BaseFloorMissing_ReturnsFailed()
    {
        var snapshotRepo = SnapshotRepoWithSingleSnapshot();
        var configRepo = Substitute.For<IFloorConfigRepository>();
        configRepo.Load().Returns(LoadedConfig(new SectionConfig
        {
            Floors = new List<FloorConfig>
            {
                DefaultFloor("F1"),
                DefaultFloor("F2")
            }
        }));

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
        configRepo.Load().Returns(LoadedConfig(new SectionConfig
        {
            Floors = new List<FloorConfig> { DefaultFloor("F1") }
        }));

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
        configRepo.Load().Returns(LoadedConfig(new SectionConfig
        {
            Floors = new List<FloorConfig> { DefaultFloor("F1") }
        }));

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
        var blockQueryService = Substitute.For<ISectionBlockQueryService>();
        blockQueryService.FindAllSectionBlockHandles().Returns(new[] { "H1" });
        snapshotRepo.Load("H1").Returns(snapshot);

        var lineResolver = Substitute.For<ISectionLineResolver>();
        lineResolver.ResolveCurrentLine("71").Returns(new Line3D(new Point3D(0, 0, 0), new Point3D(0, 20, 0)));

        var recognizer = Substitute.For<IElementRecognizer>();
        recognizer.RecognizeElements(Arg.Any<Line3D>(), Arg.Any<double>())
            .Returns(new ElementRecognitionResult { Elements = elements });

        var configRepo = Substitute.For<IFloorConfigRepository>();
        configRepo.Load().Returns(LoadedConfig(new SectionConfig
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
        }));

        var result = BuildUseCase(snapshotRepo, recognizer, configRepo, lineResolver, blockQueryService).Execute();

        result.Status.Should().Be(OperationStatus.PartialSuccess);
        result.Diagnostics.Should().Contain(d => d.Code == SectionGenerationErrorCodes.AlignmentPointsMissing);
        result.Items[0].SkippedFloors.Should().Contain("F2");
        result.Items[0].Status.Should().Be(SectionUpdateStatus.Unknown);
    }

    [Fact]
    public void Execute_MissingNonBaseScope_ReturnsUnknown()
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
            GeneratedFloorNames = new List<string> { "F1", "F2" },
            FloorSnapshots = new List<FloorSnapshot>
            {
                new() { FloorName = "F1", GeometryHash = hasher.ComputeHash(elements), ElementCount = 1 },
                new() { FloorName = "F2", GeometryHash = hasher.ComputeHash(elements), ElementCount = 1 }
            }
        };

        var snapshotRepo = Substitute.For<ISectionSnapshotRepository>();
        var blockQueryService = Substitute.For<ISectionBlockQueryService>();
        blockQueryService.FindAllSectionBlockHandles().Returns(new[] { "H1" });
        snapshotRepo.Load("H1").Returns(snapshot);

        var lineResolver = Substitute.For<ISectionLineResolver>();
        lineResolver.ResolveCurrentLine("71").Returns(new Line3D(new Point3D(0, 0, 0), new Point3D(0, 20, 0)));

        var recognizer = Substitute.For<IElementRecognizer>();
        recognizer.RecognizeElements(Arg.Any<Line3D>(), Arg.Any<double>(), Arg.Any<ScopeBounds2D?>())
            .Returns(new ElementRecognitionResult { Elements = elements });

        var configRepo = Substitute.For<IFloorConfigRepository>();
        configRepo.Load().Returns(LoadedConfig(new SectionConfig
        {
            AlignmentBaseFloorName = "F1",
            Floors = new List<FloorConfig>
            {
                DefaultFloorWithScope("F1",
                    new ScopeBounds2D { MinX = -1000, MinY = -1000, MaxX = 1000, MaxY = 1000 },
                    new Point3D(0, 0, 0),
                    new Point3D(10, 0, 0),
                    new Point3D(0, 10, 0)),
                DefaultFloorWithScope("F2", null,
                    new Point3D(100, 0, 0),
                    new Point3D(110, 0, 0),
                    new Point3D(100, 10, 0))
            }
        }));

        var result = BuildUseCase(snapshotRepo, recognizer, configRepo, lineResolver, blockQueryService).Execute();

        result.Status.Should().Be(OperationStatus.PartialSuccess);
        result.Items[0].Status.Should().Be(SectionUpdateStatus.Unknown);
        result.Items[0].SkippedFloors.Should().Contain("F2");
    }

    [Fact]
    public void Execute_SnapshotGeneratedFloorSetLimitsCurrentCheckScope()
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
        var blockQueryService = Substitute.For<ISectionBlockQueryService>();
        blockQueryService.FindAllSectionBlockHandles().Returns(new[] { "H1" });
        snapshotRepo.Load("H1").Returns(snapshot);

        var lineResolver = Substitute.For<ISectionLineResolver>();
        lineResolver.ResolveCurrentLine("71").Returns(new Line3D(new Point3D(0, 0, 0), new Point3D(0, 20, 0)));

        var recognizer = Substitute.For<IElementRecognizer>();
        recognizer.RecognizeElements(Arg.Any<Line3D>(), Arg.Any<double>())
            .Returns(new ElementRecognitionResult { Elements = elements });

        var configRepo = Substitute.For<IFloorConfigRepository>();
        configRepo.Load().Returns(LoadedConfig(new SectionConfig
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
        }));

        var result = BuildUseCase(snapshotRepo, recognizer, configRepo, lineResolver, blockQueryService).Execute();

        result.Items[0].Status.Should().Be(SectionUpdateStatus.UpToDate);
        result.Items[0].OutdatedFloors.Should().BeEmpty();
    }

    [Fact]
    public void Execute_BoundaryTemplateGeometryChangedWithSameTemplateId_ReturnsOutdated()
    {
        var elements = new[] { MakeWall() };
        var floor = DefaultFloor("F1");
        floor.BottomBoundarySlab = new BoundarySlabConfig();
        floor.TopBoundarySlab = new BoundarySlabConfig
        {
            TemplateId = "top-boundary-template"
        };

        var oldCatalog = new InMemorySlabAssemblyTemplateCatalog(new[]
        {
            CreateTopBoundaryTemplate("top-boundary-template", coreThickness: 200, finishThickness: 20)
        });
        var oldBuilder = new FloorVerticalProfileBuilder(
            oldCatalog,
            new SlabAssemblyBuilder(),
            NullLogger<FloorVerticalProfileBuilder>.Instance);
        var oldProfile = oldBuilder.Build(floor, sectionLength: 20, baseElevation: 0);
        var oldHash = SectionOutputConfigHasher.Combine(
            new FloorGeometryHasher().ComputeHash(elements, floor, oldProfile),
            new SectionOutputConfig());

        var snapshotRepo = SnapshotRepoWithSingleSnapshot(oldHash);
        var recognizer = Substitute.For<IElementRecognizer>();
        recognizer.RecognizeElements(Arg.Any<Line3D>(), Arg.Any<double>(), Arg.Any<ScopeBounds2D?>())
            .Returns(new ElementRecognitionResult { Elements = elements });

        var configRepo = Substitute.For<IFloorConfigRepository>();
        configRepo.Load().Returns(LoadedConfig(new SectionConfig
        {
            Floors = new List<FloorConfig> { floor }
        }));

        var newCatalog = new InMemorySlabAssemblyTemplateCatalog(new[]
        {
            CreateTopBoundaryTemplate("top-boundary-template", coreThickness: 260, finishThickness: 40)
        });
        var newBuilder = new FloorVerticalProfileBuilder(
            newCatalog,
            new SlabAssemblyBuilder(),
            NullLogger<FloorVerticalProfileBuilder>.Instance);

        var result = BuildUseCase(
            snapshotRepo,
            recognizer,
            configRepo,
            slabTemplateCatalog: newCatalog,
            verticalProfileBuilder: newBuilder).Execute();

        result.Items[0].Status.Should().Be(SectionUpdateStatus.Outdated);
        result.Items[0].OutdatedFloors.Should().Contain("F1");
    }

    [Fact]
    public void Execute_ConfiguredBoundaryTemplateMissing_ReturnsUnknown()
    {
        var floor = DefaultFloor("F1");
        floor.BottomBoundarySlab = new BoundarySlabConfig();
        floor.TopBoundarySlab = new BoundarySlabConfig
        {
            TemplateId = "missing-top-template"
        };

        var snapshotRepo = SnapshotRepoWithSingleSnapshot("old-hash");
        var recognizer = Substitute.For<IElementRecognizer>();
        recognizer.RecognizeElements(Arg.Any<Line3D>(), Arg.Any<double>(), Arg.Any<ScopeBounds2D?>())
            .Returns(new ElementRecognitionResult { Elements = new[] { MakeWall() } });

        var configRepo = Substitute.For<IFloorConfigRepository>();
        configRepo.Load().Returns(LoadedConfig(new SectionConfig
        {
            Floors = new List<FloorConfig> { floor }
        }));

        var catalog = new InMemorySlabAssemblyTemplateCatalog(Array.Empty<SlabAssemblyTemplate>());
        var builder = new FloorVerticalProfileBuilder(
            catalog,
            new SlabAssemblyBuilder(),
            NullLogger<FloorVerticalProfileBuilder>.Instance);

        var result = BuildUseCase(
            snapshotRepo,
            recognizer,
            configRepo,
            slabTemplateCatalog: catalog,
            verticalProfileBuilder: builder).Execute();

        result.Status.Should().Be(OperationStatus.PartialSuccess);
        result.Items[0].Status.Should().Be(SectionUpdateStatus.Unknown);
        result.Items[0].SkippedFloors.Should().Contain("F1");
        result.Diagnostics.Should().Contain(d => d.Code == FloorVerticalProfileBuilder.BoundarySlabTemplateMissingCode);
    }

    private static CheckSectionUpdatesUseCase BuildUseCase(
        ISectionSnapshotRepository? snapshotRepo = null,
        IElementRecognizer? recognizer = null,
        IFloorConfigRepository? configRepo = null,
        ISectionLineResolver? lineResolver = null,
        ISectionBlockQueryService? blockQueryService = null,
        ISlabAssemblyTemplateCatalog? slabTemplateCatalog = null,
        FloorVerticalProfileBuilder? verticalProfileBuilder = null)
    {
        snapshotRepo ??= Substitute.For<ISectionSnapshotRepository>();
        recognizer ??= Substitute.For<IElementRecognizer>();
        lineResolver ??= Substitute.For<ISectionLineResolver>();
        blockQueryService ??= Substitute.For<ISectionBlockQueryService>();
        slabTemplateCatalog ??= new InMemorySlabAssemblyTemplateCatalog();
        verticalProfileBuilder ??= new FloorVerticalProfileBuilder(
            slabTemplateCatalog,
            new SlabAssemblyBuilder(),
            NullLogger<FloorVerticalProfileBuilder>.Instance);

        if (configRepo == null)
        {
            configRepo = Substitute.For<IFloorConfigRepository>();
            configRepo.Load().Returns(LoadedConfig(new SectionConfig
            {
                Floors = new List<FloorConfig> { DefaultFloor("F1") }
            }));
        }

        return new CheckSectionUpdatesUseCase(
            snapshotRepo,
            blockQueryService,
            lineResolver,
            recognizer,
            configRepo,
            new FloorGeometryHasher(),
            slabTemplateCatalog,
            verticalProfileBuilder,
            NullLogger<CheckSectionUpdatesUseCase>.Instance);
    }

    private static ISectionSnapshotRepository SnapshotRepoWithSingleSnapshot(string? geometryHash = null)
    {
        var repo = Substitute.For<ISectionSnapshotRepository>();
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

    private static FloorConfig DefaultFloorWithScope(string name, ScopeBounds2D? scopeBounds, params Point3D[] alignmentPoints) => new()
    {
        Name = name,
        Height = 3000,
        BottomSlabThickness = 800,
        TopSlabThickness = 600,
        ScopeBounds = scopeBounds,
        AlignmentPoints = alignmentPoints.ToList()
    };

    private static LoadedSectionConfig LoadedConfig(SectionConfig config) => new()
    {
        Config = config,
        OutputConfig = new SectionOutputConfig(),
        RuntimeState = new SectionConfigRuntimeState
        {
            Source = SectionConfigStorageSource.EmbeddedDwg,
            DrawingDisplayName = "Test.dwg",
            HasPersistedConfig = true,
            IsCurrentDrawingSaved = true
        }
    };

    private static SlabAssemblyTemplate CreateTopBoundaryTemplate(
        string templateId,
        double coreThickness,
        double finishThickness)
    {
        var template = new SlabAssemblyTemplate
        {
            TemplateId = templateId,
            TemplateName = $"Template-{templateId}",
            WallJunctionMode = SlabWallJunctionMode.StopAtWallFace,
            CoreRule = new SlabCoreRule
            {
                Name = "结构顶板",
                Thickness = coreThickness,
                MaterialOrCategory = "结构",
                VisibleInSection = true
            }
        };

        if (finishThickness > 0)
        {
            template.TopLayers.Add(new SlabLayerRule
            {
                Name = "装修面层",
                Side = SlabLayerSide.Top,
                Order = 1,
                Thickness = finishThickness,
                MaterialOrCategory = "装修",
                VisibleInSection = true
            });
        }

        return template;
    }
}
