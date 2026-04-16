using FluentAssertions;
using MetroToolKits.Foundation.Building.Elements;
using MetroToolKits.Foundation.Core.Geometry;

namespace MetroToolKits.Tests.Foundation.Building;

public class SlabSectionTests
{
    private static readonly Vector3D ViewDir = new(0, 1, 0);

    private static Slab MakeRectSlab(double x0, double y0, double x1, double y1,
        double topElevation = 0, double thickness = 200)
    {
        return new Slab
        {
            Outline = new List<Point3D>
            {
                new(x0, y0, 0), new(x1, y0, 0),
                new(x1, y1, 0), new(x0, y1, 0)
            },
            TopElevation = topElevation,
            Thickness    = thickness
        };
    }

    // ── 基本剖切 ──────────────────────────────────────────────────────────────

    [Fact]
    public void Slab_HorizontalSection_ReturnsFourLines()
    {
        var slab = MakeRectSlab(0, 0, 6000, 4000, topElevation: 0, thickness: 200);
        // 剖切线沿 X 方向穿过楼板中部
        var sectionLine = new Line3D(new Point3D(-500, 2000, 0), new Point3D(6500, 2000, 0));

        var lines = slab.GetSectionGeometry(sectionLine, ViewDir).ToList();

        // 顶面线 + 底面线 + 左竖线 + 右竖线
        lines.Should().HaveCount(4);
    }

    [Fact]
    public void Slab_Section_TopAndBottomElevationsCorrect()
    {
        var slab = MakeRectSlab(0, 0, 6000, 4000, topElevation: 500, thickness: 200);
        var sectionLine = new Line3D(new Point3D(-500, 2000, 0), new Point3D(6500, 2000, 0));

        var lines = slab.GetSectionGeometry(sectionLine, ViewDir).ToList();

        // 找顶面线（Z = 500）和底面线（Z = 300）
        var topLine    = lines.FirstOrDefault(l => Math.Abs(l.Start.Z - 500) < 1e-6);
        var bottomLine = lines.FirstOrDefault(l => Math.Abs(l.Start.Z - 300) < 1e-6);

        topLine.Should().NotBeNull("应有顶面线 Z=500");
        bottomLine.Should().NotBeNull("应有底面线 Z=300");
    }

    [Fact]
    public void Slab_SectionMisses_ReturnsEmpty()
    {
        var slab = MakeRectSlab(0, 0, 6000, 4000);
        // 剖切线在楼板范围之外
        var sectionLine = new Line3D(new Point3D(-500, 5000, 0), new Point3D(6500, 5000, 0));

        var lines = slab.GetSectionGeometry(sectionLine, ViewDir).ToList();
        lines.Should().BeEmpty();
    }

    [Fact]
    public void Slab_BoundingBox_NotNull_WhenOutlineHasThreeOrMorePoints()
    {
        var slab = MakeRectSlab(0, 0, 6000, 4000);
        slab.GetBoundingBox().Should().NotBeNull();
    }

    [Fact]
    public void Slab_BoundingBox_Null_WhenOutlineTooSmall()
    {
        var slab = new Slab { Outline = new List<Point3D> { new(0, 0, 0), new(1, 0, 0) } };
        slab.GetBoundingBox().Should().BeNull();
    }
}
