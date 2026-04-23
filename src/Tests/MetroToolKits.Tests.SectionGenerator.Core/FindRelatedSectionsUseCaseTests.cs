using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using MetroToolKits.SectionGenerator.App.Abstractions;
using MetroToolKits.SectionGenerator.App.UseCases;
using MetroToolKits.SectionGenerator.Core.Sections;
using NSubstitute;

namespace MetroToolKits.Tests.SectionGenerator.Core;

public class FindRelatedSectionsUseCaseTests
{
    [Fact]
    public void Execute_SourceHandleMissing_ReturnsFailedResult()
    {
        var repo = Substitute.For<ISectionSnapshotRepository>();
        var blockQueryService = Substitute.For<ISectionBlockQueryService>();
        var useCase = new FindRelatedSectionsUseCase(repo, blockQueryService, NullLogger<FindRelatedSectionsUseCase>.Instance);

        var result = useCase.Execute(new FindRelatedSectionsRequest());

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Execute_WhenSnapshotContainsHandle_ReturnsMatchingSections()
    {
        var repo = Substitute.For<ISectionSnapshotRepository>();
        var blockQueryService = Substitute.For<ISectionBlockQueryService>();
        blockQueryService.FindAllSectionBlockHandles().Returns(new[] { "A1", "B2" });
        repo.Load("A1").Returns(new SectionSnapshot
        {
            BlockName = "剖面A",
            GeneratedAt = new DateTime(2026, 4, 20, 10, 0, 0),
            FloorSnapshots =
            {
                new FloorSnapshot
                {
                    FloorName = "F1",
                    SourceElementHandles = new List<string> { "100", "200" }
                }
            }
        });
        repo.Load("B2").Returns(new SectionSnapshot
        {
            BlockName = "剖面B",
            GeneratedAt = new DateTime(2026, 4, 20, 9, 0, 0),
            FloorSnapshots =
            {
                new FloorSnapshot
                {
                    FloorName = "F2",
                    SourceElementHandles = new List<string> { "300" }
                }
            }
        });

        var useCase = new FindRelatedSectionsUseCase(repo, blockQueryService, NullLogger<FindRelatedSectionsUseCase>.Instance);
        var result = useCase.Execute(new FindRelatedSectionsRequest { SourceHandle = "200" });

        result.Success.Should().BeTrue();
        result.Sections.Should().HaveCount(1);
        result.Sections[0].BlockHandle.Should().Be("A1");
        result.Sections[0].BlockName.Should().Be("剖面A");
        result.Sections[0].RelatedFloors.Should().ContainSingle().Which.Should().Be("F1");
    }

    [Fact]
    public void Execute_WhenMultipleMatchesExist_ReturnsByGeneratedAtDescending()
    {
        var repo = Substitute.For<ISectionSnapshotRepository>();
        var blockQueryService = Substitute.For<ISectionBlockQueryService>();
        blockQueryService.FindAllSectionBlockHandles().Returns(new[] { "A1", "B2" });
        repo.Load("A1").Returns(new SectionSnapshot
        {
            BlockName = "较早剖面",
            GeneratedAt = new DateTime(2026, 4, 20, 8, 0, 0),
            FloorSnapshots =
            {
                new FloorSnapshot
                {
                    FloorName = "F1",
                    SourceElementHandles = new List<string> { "ABC" }
                }
            }
        });
        repo.Load("B2").Returns(new SectionSnapshot
        {
            BlockName = "较新剖面",
            GeneratedAt = new DateTime(2026, 4, 20, 12, 0, 0),
            FloorSnapshots =
            {
                new FloorSnapshot
                {
                    FloorName = "F2",
                    SourceElementHandles = new List<string> { "ABC" }
                }
            }
        });

        var useCase = new FindRelatedSectionsUseCase(repo, blockQueryService, NullLogger<FindRelatedSectionsUseCase>.Instance);
        var result = useCase.Execute(new FindRelatedSectionsRequest { SourceHandle = "ABC" });

        result.Sections.Select(x => x.BlockHandle).Should().Equal("B2", "A1");
    }
}
