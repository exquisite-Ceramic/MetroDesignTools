using FluentAssertions;
using MetroToolKits.Foundation.Building.Elements;
using MetroToolKits.Foundation.Core.Geometry;
using MetroToolKits.SectionGenerator.App.Abstractions;

namespace MetroToolKits.Tests.SectionGenerator.Core;

public class SectionRecognitionSetTests
{
    [Fact]
    public void DefaultConstructor_UsesEmptyCollections()
    {
        var recognition = new SectionRecognitionSet();

        recognition.CutElements.Should().BeEmpty();
        recognition.ViewDepthCandidates.Should().BeEmpty();
        recognition.Diagnostics.Should().BeEmpty();
    }

    [Fact]
    public void ViewDepthCandidate_CarriesSourceAndStripRange()
    {
        var wall = new Wall
        {
            SourceHandle = "ABC",
            StartPoint = new Point3D(0, 10, 0),
            EndPoint = new Point3D(100, 10, 0),
            Height = 3000,
            Thickness = 200
        };

        var candidate = new ViewDepthCandidate
        {
            Element = wall,
            SourceHandle = wall.SourceHandle,
            ElementType = wall.ElementType,
            MinChainage = 10,
            MaxChainage = 100,
            MinDepth = 5,
            MaxDepth = 50
        };

        candidate.Element.Should().BeSameAs(wall);
        candidate.SourceHandle.Should().Be("ABC");
        candidate.ElementType.Should().Be("Wall");
        candidate.MinChainage.Should().Be(10);
        candidate.MaxChainage.Should().Be(100);
        candidate.MinDepth.Should().Be(5);
        candidate.MaxDepth.Should().Be(50);
    }
}
