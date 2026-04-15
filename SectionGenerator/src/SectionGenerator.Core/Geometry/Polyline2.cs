namespace SectionGenerator.Core.Geometry;

using System.Collections.Generic;

public sealed class Polyline2
{
    public Polyline2(IReadOnlyList<Vec2> vertices, bool closed = false)
    {
        Vertices = vertices;
        Closed = closed;
    }

    public IReadOnlyList<Vec2> Vertices { get; }
    public bool Closed { get; }
}

