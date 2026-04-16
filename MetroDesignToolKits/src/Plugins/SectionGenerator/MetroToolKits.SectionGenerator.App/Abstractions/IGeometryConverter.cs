using SectionGenerator.Core.Geometry;

namespace SectionGenerator.App.Abstractions;

public interface IGeometryConverter
{
    Vec2 ToUcs(Vec2 wcsPoint);
    Vec2 ToWcs(Vec2 ucsPoint);
}

