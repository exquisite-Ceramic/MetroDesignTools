using SectionGenerator.Core.Geometry;

namespace SectionGenerator.App.Abstractions;

public interface IEntityWriter
{
    void WritePolyline2(Polyline2 polyline, string layer);
}

