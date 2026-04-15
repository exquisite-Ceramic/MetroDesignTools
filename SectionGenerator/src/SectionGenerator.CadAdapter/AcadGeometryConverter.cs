using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using SectionGenerator.App.Abstractions;
using SectionGenerator.Core.Geometry;

namespace SectionGenerator.CadAdapter;

public sealed class AcadGeometryConverter : IGeometryConverter
{
    private readonly Editor _ed;

    public AcadGeometryConverter(Editor ed)
    {
        _ed = ed;
    }

    public Vec2 ToUcs(Vec2 wcsPoint)
    {
        Matrix3d ucs = _ed.CurrentUserCoordinateSystem;
        Matrix3d inv = ucs.Inverse();
        Point3d p = new Point3d(wcsPoint.X, wcsPoint.Y, 0).TransformBy(inv);
        return new Vec2(p.X, p.Y);
    }

    public Vec2 ToWcs(Vec2 ucsPoint)
    {
        Matrix3d ucs = _ed.CurrentUserCoordinateSystem;
        Point3d p = new Point3d(ucsPoint.X, ucsPoint.Y, 0).TransformBy(ucs);
        return new Vec2(p.X, p.Y);
    }
}

