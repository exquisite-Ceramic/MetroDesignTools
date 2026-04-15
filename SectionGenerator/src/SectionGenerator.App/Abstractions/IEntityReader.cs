using SectionGenerator.App.Abstractions.Selection;
using SectionGenerator.Core.Geometry;

namespace SectionGenerator.App.Abstractions;

public interface IEntityReader
{
    Polyline2 ReadPolyline2(SelectedObjectRef obj);
}

