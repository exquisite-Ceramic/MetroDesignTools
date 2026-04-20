using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using MetroToolKits.SectionGenerator.App.Abstractions;
using MetroToolKits.SectionGenerator.App.UseCases;
using NSubstitute;

namespace MetroToolKits.Tests.SectionGenerator.Core;

public class LocateSourceElementUseCaseTests
{
    [Fact]
    public void Execute_WithoutHandle_ReturnsFailedResult()
    {
        var repo = Substitute.For<ISectionReferenceRepository>();
        var useCase = new LocateSourceElementUseCase(repo, NullLogger<LocateSourceElementUseCase>.Instance);

        var result = useCase.Execute(new LocateSourceElementRequest());

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Execute_WhenReferenceExists_ReturnsSourceHandleAndType()
    {
        var repo = Substitute.For<ISectionReferenceRepository>();
        repo.GetSectionEntityReference("1A2B").Returns(new SectionEntityReference
        {
            SourceHandle = "9F8E",
            ElementType = "Wall"
        });

        var useCase = new LocateSourceElementUseCase(repo, NullLogger<LocateSourceElementUseCase>.Instance);
        var result = useCase.Execute(new LocateSourceElementRequest { SectionEntityHandle = "1A2B" });

        result.Success.Should().BeTrue();
        result.SourceHandle.Should().Be("9F8E");
        result.ElementType.Should().Be("Wall");
    }

    [Fact]
    public void Execute_WhenReferenceMissing_ReturnsFailedResult()
    {
        var repo = Substitute.For<ISectionReferenceRepository>();
        repo.GetSectionEntityReference("1A2B").Returns((SectionEntityReference?)null);

        var useCase = new LocateSourceElementUseCase(repo, NullLogger<LocateSourceElementUseCase>.Instance);
        var result = useCase.Execute(new LocateSourceElementRequest { SectionEntityHandle = "1A2B" });

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("所选剖面实体没有源构件引用");
    }
}
