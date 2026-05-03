using FluentAssertions;
using MetroToolKits.Foundation.Core.Geometry;
using MetroToolKits.SectionGenerator.Core.Sections;
using MetroToolKits.SectionGenerator.Core.Sections.Hatching;

namespace MetroToolKits.Tests.SectionGenerator.Core.Sections.Hatching;

public class SectionHatchRegionFactoryTests
{
    [Fact]
    public void TryCreateQuad_ValidRectangle_CreatesRegion()
    {
        var created = SectionHatchRegionFactory.TryCreateQuad(
            SectionHatchCategory.Wall,
            new Point3D(0, 0, 0),
            new Point3D(1000, 0, 0),
            new Point3D(1000, 500, 0),
            new Point3D(0, 500, 0),
            out var region,
            out var validation);

        created.Should().BeTrue();
        validation.IsValid.Should().BeTrue();
        region.Should().NotBeNull();
        region!.Category.Should().Be(SectionHatchCategory.Wall);
        region.Boundary.Should().HaveCount(4);
    }

    [Fact]
    public void TryCreateQuad_ZeroArea_ReturnsFalse()
    {
        var created = SectionHatchRegionFactory.TryCreateQuad(
            SectionHatchCategory.Column,
            new Point3D(0, 0, 0),
            new Point3D(1000, 0, 0),
            new Point3D(2000, 0, 0),
            new Point3D(3000, 0, 0),
            out var region,
            out var validation);

        created.Should().BeFalse();
        region.Should().BeNull();
        validation.IsValid.Should().BeFalse();
        validation.IssueCode.Should().Be(HatchBoundaryIssueCode.ZeroArea);
    }

    [Fact]
    public void TryCreate_UsesNormalizedBoundary()
    {
        var boundary = new[]
        {
            new Point3D(0, 0, 0),
            new Point3D(1000, 500, 0),
            new Point3D(1000, 0, 0),
            new Point3D(0, 500, 0),
            new Point3D(0, 0, 0)
        };

        var created = SectionHatchRegionFactory.TryCreate(
            SectionHatchCategory.Slab,
            boundary,
            out var region,
            out var validation);

        created.Should().BeTrue();
        validation.IsValid.Should().BeTrue();
        region.Should().NotBeNull();
        region!.Boundary.Should().Equal(validation.NormalizedBoundary);
        region.Boundary.Should().HaveCount(4);
    }
}
