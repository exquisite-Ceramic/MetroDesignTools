using Autodesk.AutoCAD.DatabaseServices;
using SectionGenerator.App.Abstractions;
using SectionGenerator.App.Abstractions.Selection;
using SectionGenerator.Core.Geometry;
using System;
using System.Collections.Generic;

namespace SectionGenerator.CadAdapter;

public sealed class AcadEntityReader : IEntityReader
{
    private readonly Database _db;
    private readonly Transaction _tr;

    public AcadEntityReader(Database db, Transaction tr)
    {
        _db = db;
        _tr = tr;
    }

    public Polyline2 ReadPolyline2(SelectedObjectRef obj)
    {
        ObjectId id = AcadHandleResolver.Resolve(_db, obj.Handle);
        if (id.IsNull)
            return new Polyline2(Array.Empty<Vec2>());

        DBObject dbo = _tr.GetObject(id, OpenMode.ForRead, false);
        if (dbo is not Polyline pl)
            return new Polyline2(Array.Empty<Vec2>());

        var pts = new List<Vec2>(pl.NumberOfVertices);
        for (int i = 0; i < pl.NumberOfVertices; i++)
        {
            var p = pl.GetPoint2dAt(i);
            pts.Add(new Vec2(p.X, p.Y));
        }

        return new Polyline2(pts, pl.Closed);
    }
}

