using Autodesk.AutoCAD.DatabaseServices;
using SectionGenerator.App.Abstractions;
using SectionGenerator.Core.Geometry;

namespace SectionGenerator.CadAdapter;

public sealed class AcadEntityWriter : IEntityWriter
{
    private readonly Database _db;
    private readonly Transaction _tr;

    public AcadEntityWriter(Database db, Transaction tr)
    {
        _db = db;
        _tr = tr;
    }

    public void WritePolyline2(Polyline2 polyline, string layer)
    {
        if (polyline.Vertices.Count <= 0)
            return;

        var pl = new Polyline();
        for (int i = 0; i < polyline.Vertices.Count; i++)
        {
            Vec2 p = polyline.Vertices[i];
            pl.AddVertexAt(i, new Autodesk.AutoCAD.Geometry.Point2d(p.X, p.Y), 0, 0, 0);
        }
        pl.Closed = polyline.Closed;
        if (!string.IsNullOrWhiteSpace(layer))
            pl.Layer = layer;

        BlockTable bt = (BlockTable)_tr.GetObject(_db.BlockTableId, OpenMode.ForRead);
        BlockTableRecord ms = (BlockTableRecord)_tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
        ms.AppendEntity(pl);
        _tr.AddNewlyCreatedDBObject(pl, true);
    }
}

