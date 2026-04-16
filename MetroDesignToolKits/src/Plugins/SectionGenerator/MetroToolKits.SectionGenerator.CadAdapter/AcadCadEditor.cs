using Autodesk.AutoCAD.EditorInput;
using SectionGenerator.App.Abstractions;

namespace SectionGenerator.CadAdapter;

public sealed class AcadCadEditor : ICadEditor
{
    private readonly Editor _ed;

    public AcadCadEditor(Editor ed)
    {
        _ed = ed;
    }

    public void WriteMessage(string message) => _ed.WriteMessage(message);
}

