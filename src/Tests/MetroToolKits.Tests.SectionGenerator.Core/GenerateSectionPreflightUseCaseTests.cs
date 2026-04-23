using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using MetroToolKits.Foundation.Core.Diagnostics;
using MetroToolKits.Foundation.Core.Geometry;
using MetroToolKits.SectionGenerator.App.Abstractions;
using MetroToolKits.SectionGenerator.App.Models;
using MetroToolKits.SectionGenerator.App.UseCases;
using MetroToolKits.SectionGenerator.Core.Sections;
using NSubstitute;

namespace MetroToolKits.Tests.SectionGenerator.Core;

public class GenerateSectionPreflightUseCaseTests
{
    [Fact]
    public void Execute_WhenMultiFloorConfigMissingBaseScope_ReturnsBlockingRequirement()
    {
        var repository = Substitute.For<IFloorConfigRepository>();
        repository.Load().Returns(new LoadedSectionConfig
        {
            Config = new SectionConfig
            {
                AlignmentBaseFloorName = "F1",
                Floors = new List<FloorConfig>
                {
                    new()
                    {
                        Name = "F1",
                        AlignmentPoints = new List<Point3D>
                        {
                            new(0, 0, 0),
                            new(10, 0, 0),
                            new(0, 10, 0)
                        }
                    },
                    new()
                    {
                        Name = "F2"
                    }
                }
            }
        });

        var checkUseCase = Substitute.For<ICheckSectionUpdatesUseCase>();
        checkUseCase.Execute().Returns(new CheckSectionUpdatesResult
        {
            Status = OperationStatus.Success
        });

        var useCase = new GenerateSectionPreflightUseCase(
            repository,
            checkUseCase,
            NullLogger<GenerateSectionPreflightUseCase>.Instance);

        var result = useCase.Execute();

        result.CanGenerate.Should().BeFalse();
        result.MissingRequirements.Should().ContainSingle();
        result.MissingRequirements[0].Message.Should().Contain("整层范围框");
    }

    [Fact]
    public void Execute_WhenExistingSectionsChecked_BuildsSummaryCounts()
    {
        var repository = Substitute.For<IFloorConfigRepository>();
        repository.Load().Returns(new LoadedSectionConfig
        {
            Config = new SectionConfig
            {
                Floors = new List<FloorConfig>
                {
                    new()
                    {
                        Name = "F1"
                    }
                }
            }
        });

        var checkUseCase = Substitute.For<ICheckSectionUpdatesUseCase>();
        checkUseCase.Execute().Returns(new CheckSectionUpdatesResult
        {
            Status = OperationStatus.PartialSuccess,
            Items = new[]
            {
                new SectionCheckResult
                {
                    BlockHandle = "A",
                    BlockName = "A",
                    Status = SectionUpdateStatus.UpToDate
                },
                new SectionCheckResult
                {
                    BlockHandle = "B",
                    BlockName = "B",
                    Status = SectionUpdateStatus.Outdated
                },
                new SectionCheckResult
                {
                    BlockHandle = "C",
                    BlockName = "C",
                    Status = SectionUpdateStatus.Unknown,
                    SkippedFloors = new List<string> { "F2" }
                }
            }
        });

        var useCase = new GenerateSectionPreflightUseCase(
            repository,
            checkUseCase,
            NullLogger<GenerateSectionPreflightUseCase>.Instance);

        var result = useCase.Execute();

        result.ExistingSections.TotalCount.Should().Be(3);
        result.ExistingSections.UpToDateCount.Should().Be(1);
        result.ExistingSections.OutdatedCount.Should().Be(1);
        result.ExistingSections.UnknownCount.Should().Be(1);
        result.ExistingSections.PartialCount.Should().Be(1);
        result.ExistingSections.CheckResult.Should().NotBeNull();
    }
}
