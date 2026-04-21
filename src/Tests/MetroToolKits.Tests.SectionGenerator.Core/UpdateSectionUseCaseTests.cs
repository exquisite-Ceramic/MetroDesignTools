using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using MetroToolKits.Foundation.Core.Geometry;
using MetroToolKits.Foundation.Core.Logging;
using MetroToolKits.SectionGenerator.App.Abstractions;
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
            ViewDepth = 3000
        };

        var resolvedLine = new Line3D(new Point3D(50, 0, 0), new Point3D(50, 20, 0));

        var snapshotRepo = Substitute.For<ISectionSnapshotRepository>();
        snapshotRepo.Load("85").Returns(snapshot);

        var lineResolver = Substitute.For<ISectionLineResolver>();
        lineResolver.ResolveCurrentLine("71").Returns(resolvedLine);

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
            lineResolver,
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
            request.ViewDepth == 3000));
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

        var lineResolver = Substitute.For<ISectionLineResolver>();
        lineResolver.ResolveCurrentLine("71").Returns((Line3D?)null);

        var generateUseCase = Substitute.For<IGenerateSectionUseCase>();
        var eraseService = Substitute.For<IBlockEraseService>();
        var userLogger = Substitute.For<IUserLogger>();

        var useCase = new UpdateSectionUseCase(
            snapshotRepo,
            lineResolver,
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
}
