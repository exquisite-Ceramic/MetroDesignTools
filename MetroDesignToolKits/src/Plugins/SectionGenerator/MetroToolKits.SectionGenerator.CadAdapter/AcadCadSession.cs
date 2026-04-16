using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.EditorInput;
using SectionGenerator.App.Abstractions;

namespace SectionGenerator.CadAdapter;

public sealed class AcadCadSession : ICadSession
{
    public AcadCadSession(Document doc)
    {
        Document = doc;
        EditorRaw = doc.Editor;
        Editor = new AcadCadEditor(EditorRaw);
    }

    public Document Document { get; }
    public Editor EditorRaw { get; }
    public ICadEditor Editor { get; }
}

