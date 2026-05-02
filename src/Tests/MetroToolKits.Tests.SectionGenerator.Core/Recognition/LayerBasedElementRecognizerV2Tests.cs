using FluentAssertions;
using MetroToolKits.Foundation.Building.Elements;
using MetroToolKits.Foundation.Core.Geometry;
using MetroToolKits.SectionGenerator.App.Abstractions;
using MetroToolKits.SectionGenerator.Infrastructure.Recognition;

namespace MetroToolKits.Tests.SectionGenerator.Core.Recognition;

public class LayerBasedElementRecognizerV2Tests
{
    private static readonly Line3D SectionLine = new(new Point3D(0, 0, 0), new Point3D(100, 0, 0));
    private static readonly Vector3D ViewDirection = new(0, 1, 0);

    [Fact]
    public void Type_ImplementsSectionElementRecognizerV2()
    {
        typeof(LayerBasedElementRecognizer)
            .Should()
            .BeAssignableTo<ISectionElementRecognizerV2>();
    }

    [Fact]
    public void ClassifyElementForSection_IntersectingElement_IsCutElement()
    {
        var wall = Wall("CUT", startX: 50, startY: -10, endX: 50, endY: 10);

        var result = LayerBasedElementRecognizer.ClassifyElementForSection(
            wall,
            SectionLine,
            ViewDirection,
            viewDepth: 100,
            scopeBounds: null);

        result.IntersectsSection.Should().BeTrue();
        result.ViewDepthCandidate.Should().BeNull();
    }

    [Fact]
    public void ClassifyElementForSection_NonIntersectingElementInsideViewDepth_IsCandidate()
    {
        var wall = Wall("CANDIDATE", startX: 20, startY: 20, endX: 40, endY: 20);

        var result = LayerBasedElementRecognizer.ClassifyElementForSection(
            wall,
            SectionLine,
            ViewDirection,
            viewDepth: 100,
            scopeBounds: null);

        result.IntersectsSection.Should().BeFalse();
        result.ViewDepthCandidate.Should().NotBeNull();
        result.ViewDepthCandidate!.Element.Should().BeSameAs(wall);
        result.ViewDepthCandidate.SourceHandle.Should().Be("CANDIDATE");
        result.ViewDepthCandidate.ElementType.Should().Be("Wall");
        result.ViewDepthCandidate.MinChainage.Should().BeApproximately(20, 1e-6);
        result.ViewDepthCandidate.MaxChainage.Should().BeApproximately(40, 1e-6);
        result.ViewDepthCandidate.MinDepth.Should().BeGreaterThan(0);
        result.ViewDepthCandidate.MaxDepth.Should().BeLessThan(100);
    }

    [Fact]
    public void ClassifyElementForSection_BackSideElement_IsNotCandidate()
    {
        var wall = Wall("BACK", startX: 20, startY: -20, endX: 40, endY: -20);

        var result = LayerBasedElementRecognizer.ClassifyElementForSection(
            wall,
            SectionLine,
            ViewDirection,
            viewDepth: 100,
            scopeBounds: null);

        result.IntersectsSection.Should().BeFalse();
        result.ViewDepthCandidate.Should().BeNull();
    }

    [Fact]
    public void ClassifyElementForSection_ElementBeyondViewDepth_IsNotCandidate()
    {
        var wall = Wall("FAR", startX: 20, startY: 500, endX: 40, endY: 500);

        var result = LayerBasedElementRecognizer.ClassifyElementForSection(
            wall,
            SectionLine,
            ViewDirection,
            viewDepth: 100,
            scopeBounds: null);

        result.IntersectsSection.Should().BeFalse();
        result.ViewDepthCandidate.Should().BeNull();
    }

    [Fact]
    public void ClassifyElementForSection_CandidateCountDiffersByViewDepth()
    {
        var elements = new[]
        {
            Wall("NEAR", startX: 20, startY: 50, endX: 40, endY: 50),
            Wall("FAR", startX: 20, startY: 500, endX: 40, endY: 500)
        };

        var candidatesAt100 = elements
            .Select(element => LayerBasedElementRecognizer.ClassifyElementForSection(
                element,
                SectionLine,
                ViewDirection,
                viewDepth: 100,
                scopeBounds: null).ViewDepthCandidate)
            .Count(candidate => candidate != null);

        var candidatesAt3000 = elements
            .Select(element => LayerBasedElementRecognizer.ClassifyElementForSection(
                element,
                SectionLine,
                ViewDirection,
                viewDepth: 3000,
                scopeBounds: null).ViewDepthCandidate)
            .Count(candidate => candidate != null);

        candidatesAt100.Should().Be(1);
        candidatesAt3000.Should().Be(2);
    }

    [Fact]
    public void ClassifyElementForSection_NonPositiveViewDepth_IsSafeAndHasNoCandidate()
    {
        var wall = Wall("CANDIDATE", startX: 20, startY: 20, endX: 40, endY: 20);

        var result = LayerBasedElementRecognizer.ClassifyElementForSection(
            wall,
            SectionLine,
            ViewDirection,
            viewDepth: 0,
            scopeBounds: null);

        result.IntersectsSection.Should().BeFalse();
        result.ViewDepthCandidate.Should().BeNull();
    }

    private static Wall Wall(string handle, double startX, double startY, double endX, double endY)
        => new()
        {
            SourceHandle = handle,
            StartPoint = new Point3D(startX, startY, 0),
            EndPoint = new Point3D(endX, endY, 0),
            Height = 3000,
            Thickness = 2
        };
}
