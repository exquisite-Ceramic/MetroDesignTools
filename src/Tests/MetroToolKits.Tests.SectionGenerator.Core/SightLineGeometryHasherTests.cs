using FluentAssertions;
using MetroToolKits.Foundation.Core.Geometry;
using MetroToolKits.SectionGenerator.Core.Sections;

namespace MetroToolKits.Tests.SectionGenerator.Core;

public class SightLineGeometryHasherTests
{
    [Fact]
    public void ComputeHash_SameInput_IsStable()
    {
        var hasher = new SightLineGeometryHasher();
        var input = new[] { MakeSightLineElement("A", 0, 10) };

        hasher.ComputeHash(input).Should().Be(hasher.ComputeHash(input));
    }

    [Fact]
    public void ComputeHash_CoordinateChanged_Differs()
    {
        var hasher = new SightLineGeometryHasher();
        var original = hasher.ComputeHash(new[] { MakeSightLineElement("A", 0, 10) });
        var changed = hasher.ComputeHash(new[] { MakeSightLineElement("A", 0, 20) });

        changed.Should().NotBe(original);
    }

    [Fact]
    public void ComputeHash_SourceHandleChanged_Differs()
    {
        var hasher = new SightLineGeometryHasher();
        var original = hasher.ComputeHash(new[] { MakeSightLineElement("A", 0, 10) });
        var changed = hasher.ComputeHash(new[] { MakeSightLineElement("B", 0, 10) });

        changed.Should().NotBe(original);
    }

    [Fact]
    public void ComputeHash_EmptyInput_ReturnsStableNonEmptyHash()
    {
        var hasher = new SightLineGeometryHasher();
        var first = hasher.ComputeHash(Array.Empty<ElementSectionData>());
        var second = hasher.ComputeHash(Array.Empty<ElementSectionData>());

        first.Should().NotBeNullOrWhiteSpace();
        first.Should().Be(second);
    }

    private static ElementSectionData MakeSightLineElement(string sourceHandle, double startX, double endX)
        => new()
        {
            SourceHandle = sourceHandle,
            SourceHandles = new[] { sourceHandle },
            ElementType = "Wall",
            SightLines = new[]
            {
                new Line3D(new Point3D(startX, 0, 0), new Point3D(endX, 3000, 0))
            }
        };
}
