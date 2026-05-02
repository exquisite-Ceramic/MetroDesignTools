using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using MetroToolKits.Foundation.Core.Geometry;
using MetroToolKits.Foundation.Core.Logging;
using MetroToolKits.SectionGenerator.App.Abstractions;
using MetroToolKits.SectionGenerator.App.Models;
using MetroToolKits.SectionGenerator.App.UseCases;
using MetroToolKits.SectionGenerator.Core.Sections;
using NSubstitute;

namespace MetroToolKits.Tests.SectionGenerator.Core;

public class UpdateSectionUseCaseTests
{
    [Fact]
    public void Execute_UsesResolvedCurrentSourceLine_WhenUpdatingSection()
    {
        var snapshot = new SectionSnapshot
        {
            BlockName = "MK_剖面_F1",
            SourceCutLineHandle = "71",
            CutLineStart = new Point3D(0, 0, 0),
            CutLineEnd = new Point3D(0, 20, 0),
            InsertionPoint = new Point3D(100, 200, 0),
            GeometryAnchorX = 0,
            SectionDirection = new Point3D(0, 1, 0),
            ViewDepth = 4321,
            TargetFloorName = "F1",
            LocalScopeFloorName = "F1",
            LocalScopeBounds = new ScopeBounds2D
            {
                MinX = 10,
                MinY = 20,
                MaxX = 30,
                MaxY = 40
            },
            GeneratedFloorNames = new List<string> { "F1" }
        };

        var resolvedLine = new Line3D(new Point3D(50, 0, 0), new Point3D(50, 20, 0));

        var snapshotRepo = Substitute.For<ISectionSnapshotRepository>();
        snapshotRepo.Load("85").Returns(snapshot);
        var geometryRecoveryService = Substitute.For<ISectionGeometryRecoveryService>();

        var lineResolver = Substitute.For<ISectionLineResolver>();
        lineResolver.ResolveCurrentLine("71").Returns(resolvedLine);
        var configRepo = Substitute.For<IFloorConfigRepository>();
        configRepo.Load().Returns(EmbeddedConfig());

        var generateUseCase = Substitute.For<IGenerateSectionUseCase>();
        generateUseCase.Execute(Arg.Any<GenerateSectionRequest>())
            .Returns(new GenerateSectionResult
            {
                Status = Foundation.Core.Diagnostics.OperationStatus.Success,
                BlockName = "MK_剖面_F1_updated",
                BlockHandle = "90"
            });

        var eraseService = Substitute.For<IBlockEraseService>();
        var userLogger = Substitute.For<IUserLogger>();

        var useCase = new UpdateSectionUseCase(
            snapshotRepo,
            geometryRecoveryService,
            lineResolver,
            configRepo,
            generateUseCase,
            eraseService,
            NullLogger<UpdateSectionUseCase>.Instance,
            userLogger);

        var result = useCase.Execute(new UpdateSectionRequest { BlockHandle = "85" });

        result.Success.Should().BeTrue();
        generateUseCase.Received(1).Execute(Arg.Is<GenerateSectionRequest>(request =>
            request.CutLineHandle == "71" &&
            request.CutLineStart.Equals(resolvedLine.Start) &&
            request.CutLineEnd.Equals(resolvedLine.End) &&
            request.InsertionPoint.Equals(snapshot.InsertionPoint) &&
            request.GeometryAnchorX == 0 &&
            request.ViewDepth == 4321 &&
            request.TargetFloorName == "F1" &&
            request.LocalScopeFloorName == "F1" &&
            request.LocalScopeBounds.HasValue &&
            snapshot.LocalScopeBounds.HasValue &&
            request.LocalScopeBounds.Value.Equals(snapshot.LocalScopeBounds.Value) &&
            request.RequireCompleteIncludedFloors &&
            request.IncludedFloorNames.SequenceEqual(new[] { "F1" })));
        eraseService.Received(1).EraseBlock("85");
    }

    [Fact]
    public void Execute_WhenSourceLineCannotBeResolved_ReturnsFailure()
    {
        var snapshot = new SectionSnapshot
        {
            BlockName = "MK_剖面_F1",
            SourceCutLineHandle = "71"
        };

        var snapshotRepo = Substitute.For<ISectionSnapshotRepository>();
        snapshotRepo.Load("85").Returns(snapshot);
        var geometryRecoveryService = Substitute.For<ISectionGeometryRecoveryService>();

        var lineResolver = Substitute.For<ISectionLineResolver>();
        lineResolver.ResolveCurrentLine("71").Returns((Line3D?)null);

        var configRepo = Substitute.For<IFloorConfigRepository>();
        var generateUseCase = Substitute.For<IGenerateSectionUseCase>();
        var eraseService = Substitute.For<IBlockEraseService>();
        var userLogger = Substitute.For<IUserLogger>();

        var useCase = new UpdateSectionUseCase(
            snapshotRepo,
            geometryRecoveryService,
            lineResolver,
            configRepo,
            generateUseCase,
            eraseService,
            NullLogger<UpdateSectionUseCase>.Instance,
            userLogger);

        var result = useCase.Execute(new UpdateSectionRequest { BlockHandle = "85" });

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("原始剖切线已不存在或不是直线");
        generateUseCase.DidNotReceive().Execute(Arg.Any<GenerateSectionRequest>());
        eraseService.DidNotReceive().EraseBlock(Arg.Any<string>());
    }

    [Fact]
    public void Execute_WhenGenerateReturnsFailure_DoesNotEraseOldBlock()
    {
        var snapshot = new SectionSnapshot
        {
            BlockName = "MK_剖面_F1",
            SourceCutLineHandle = "71",
            InsertionPoint = new Point3D(100, 200, 0),
            GeometryAnchorX = 0,
            SectionDirection = new Point3D(0, 1, 0),
            ViewDepth = 3000
        };

        var resolvedLine = new Line3D(new Point3D(50, 0, 0), new Point3D(50, 20, 0));

        var snapshotRepo = Substitute.For<ISectionSnapshotRepository>();
        snapshotRepo.Load("85").Returns(snapshot);
        var geometryRecoveryService = Substitute.For<ISectionGeometryRecoveryService>();

        var lineResolver = Substitute.For<ISectionLineResolver>();
        lineResolver.ResolveCurrentLine("71").Returns(resolvedLine);
        var configRepo = Substitute.For<IFloorConfigRepository>();
        configRepo.Load().Returns(EmbeddedConfig());

        var generateUseCase = Substitute.For<IGenerateSectionUseCase>();
        generateUseCase.Execute(Arg.Any<GenerateSectionRequest>())
            .Returns(new GenerateSectionResult
            {
                Status = Foundation.Core.Diagnostics.OperationStatus.Failed,
                Failure = new Foundation.Core.Diagnostics.OperationFailure
                {
                    Code = "SectionGenerator.FloorConfigLoad.AlignmentBaseFloorInvalid",
                    UserMessage = "基准层配置无效"
                }
            });

        var eraseService = Substitute.For<IBlockEraseService>();
        var userLogger = Substitute.For<IUserLogger>();

        var useCase = new UpdateSectionUseCase(
            snapshotRepo,
            geometryRecoveryService,
            lineResolver,
            configRepo,
            generateUseCase,
            eraseService,
            NullLogger<UpdateSectionUseCase>.Instance,
            userLogger);

        var result = useCase.Execute(new UpdateSectionRequest { BlockHandle = "85" });

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("基准层配置无效");
        eraseService.DidNotReceive().EraseBlock(Arg.Any<string>());
    }

    [Fact]
    public void Execute_WhenSnapshotAnchorMissing_UsesResolvedBlockGeometryAnchor()
    {
        var snapshot = new SectionSnapshot
        {
            BlockName = "MK_剖面_F1",
            SourceCutLineHandle = "71",
            CutLineStart = new Point3D(0, 0, 0),
            CutLineEnd = new Point3D(0, 20, 0),
            InsertionPoint = new Point3D(100, 200, 0),
            ViewDepth = 3000
        };

        var resolvedLine = new Line3D(new Point3D(50, 0, 0), new Point3D(50, 20, 0));
        var snapshotRepo = Substitute.For<ISectionSnapshotRepository>();
        snapshotRepo.Load("85").Returns(snapshot);
        var geometryRecoveryService = Substitute.For<ISectionGeometryRecoveryService>();
        geometryRecoveryService.ResolveBlockGeometryAnchorX("85").Returns(0d);

        var lineResolver = Substitute.For<ISectionLineResolver>();
        lineResolver.ResolveCurrentLine("71").Returns(resolvedLine);
        var configRepo = Substitute.For<IFloorConfigRepository>();
        configRepo.Load().Returns(EmbeddedConfig());

        var generateUseCase = Substitute.For<IGenerateSectionUseCase>();
        generateUseCase.Execute(Arg.Any<GenerateSectionRequest>())
            .Returns(new GenerateSectionResult
            {
                Status = Foundation.Core.Diagnostics.OperationStatus.Success,
                BlockName = "MK_剖面_F1_updated",
                BlockHandle = "90"
            });

        var eraseService = Substitute.For<IBlockEraseService>();
        var userLogger = Substitute.For<IUserLogger>();

        var useCase = new UpdateSectionUseCase(
            snapshotRepo,
            geometryRecoveryService,
            lineResolver,
            configRepo,
            generateUseCase,
            eraseService,
            NullLogger<UpdateSectionUseCase>.Instance,
            userLogger);

        var result = useCase.Execute(new UpdateSectionRequest { BlockHandle = "85" });

        result.Success.Should().BeTrue();
        geometryRecoveryService.Received(1).ResolveBlockGeometryAnchorX("85");
        generateUseCase.Received(1).Execute(Arg.Is<GenerateSectionRequest>(request =>
            request.GeometryAnchorX == 0));
    }

    [Fact]
    public void Execute_PrefersExecutionFloorNamesOverGeneratedFloorNames()
    {
        var snapshot = new SectionSnapshot
        {
            BlockName = "MK_剖面_F1_F3",
            SourceCutLineHandle = "71",
            CutLineStart = new Point3D(0, 0, 0),
            CutLineEnd = new Point3D(0, 20, 0),
            InsertionPoint = new Point3D(100, 200, 0),
            GeometryAnchorX = 0,
            SectionDirection = new Point3D(0, 1, 0),
            ViewDepth = 3000,
            ExecutionFloorNames = new List<string> { "F1", "F2", "F3" },
            GeneratedFloorNames = new List<string> { "F1", "F3" }
        };

        var resolvedLine = new Line3D(new Point3D(50, 0, 0), new Point3D(50, 20, 0));
        var snapshotRepo = Substitute.For<ISectionSnapshotRepository>();
        snapshotRepo.Load("85").Returns(snapshot);
        var geometryRecoveryService = Substitute.For<ISectionGeometryRecoveryService>();

        var lineResolver = Substitute.For<ISectionLineResolver>();
        lineResolver.ResolveCurrentLine("71").Returns(resolvedLine);
        var configRepo = Substitute.For<IFloorConfigRepository>();
        configRepo.Load().Returns(EmbeddedConfig());

        var generateUseCase = Substitute.For<IGenerateSectionUseCase>();
        generateUseCase.Execute(Arg.Any<GenerateSectionRequest>())
            .Returns(new GenerateSectionResult
            {
                Status = Foundation.Core.Diagnostics.OperationStatus.Success,
                BlockName = "MK_剖面_F1_F3_updated",
                BlockHandle = "90"
            });

        var eraseService = Substitute.For<IBlockEraseService>();
        var userLogger = Substitute.For<IUserLogger>();

        var useCase = new UpdateSectionUseCase(
            snapshotRepo,
            geometryRecoveryService,
            lineResolver,
            configRepo,
            generateUseCase,
            eraseService,
            NullLogger<UpdateSectionUseCase>.Instance,
            userLogger);

        var result = useCase.Execute(new UpdateSectionRequest { BlockHandle = "85" });

        result.Success.Should().BeTrue();
        generateUseCase.Received(1).Execute(Arg.Is<GenerateSectionRequest>(request =>
            request.IncludedFloorNames.SequenceEqual(new[] { "F1", "F2", "F3" })));
    }

    [Fact]
    public void Execute_WhenEmbeddedConfigMissing_ReturnsFailureWithoutRegenerating()
    {
        var snapshot = new SectionSnapshot
        {
            BlockName = "MK_剖面_F1",
            SourceCutLineHandle = "71",
            CutLineStart = new Point3D(0, 0, 0),
            CutLineEnd = new Point3D(0, 20, 0),
            InsertionPoint = new Point3D(100, 200, 0),
            ViewDepth = 3000
        };

        var resolvedLine = new Line3D(new Point3D(50, 0, 0), new Point3D(50, 20, 0));
        var snapshotRepo = Substitute.For<ISectionSnapshotRepository>();
        snapshotRepo.Load("85").Returns(snapshot);
        var geometryRecoveryService = Substitute.For<ISectionGeometryRecoveryService>();

        var lineResolver = Substitute.For<ISectionLineResolver>();
        lineResolver.ResolveCurrentLine("71").Returns(resolvedLine);
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

        var generateUseCase = Substitute.For<IGenerateSectionUseCase>();
        var eraseService = Substitute.For<IBlockEraseService>();
        var userLogger = Substitute.For<IUserLogger>();

        var useCase = new UpdateSectionUseCase(
            snapshotRepo,
            geometryRecoveryService,
            lineResolver,
            configRepo,
            generateUseCase,
            eraseService,
            NullLogger<UpdateSectionUseCase>.Instance,
            userLogger);

        var result = useCase.Execute(new UpdateSectionRequest { BlockHandle = "85" });

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("当前图纸缺少内嵌楼层配置，请先打开 FloorConfig 保存当前图纸配置后再更新剖面");
        generateUseCase.DidNotReceive().Execute(Arg.Any<GenerateSectionRequest>());
        eraseService.DidNotReceive().EraseBlock(Arg.Any<string>());
    }

    private static LoadedSectionConfig EmbeddedConfig() => new()
    {
        Config = new SectionConfig(),
        OutputConfig = new SectionOutputConfig(),
        RuntimeState = new SectionConfigRuntimeState
        {
            Source = SectionConfigStorageSource.EmbeddedDwg,
            DrawingDisplayName = "Test.dwg",
            HasPersistedConfig = true,
            IsCurrentDrawingSaved = true
        }
    };
}
