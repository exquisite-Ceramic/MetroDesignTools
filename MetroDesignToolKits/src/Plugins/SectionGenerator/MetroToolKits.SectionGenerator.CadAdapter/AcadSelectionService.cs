using Autodesk.AutoCAD.EditorInput;
using SectionGenerator.App.Abstractions;
using SectionGenerator.App.Abstractions.Selection;
using System;
using System.Collections.Generic;

namespace SectionGenerator.CadAdapter;

public sealed class AcadSelectionService : ISelectionService
{
    private readonly Editor _ed;

    public AcadSelectionService(Editor ed)
    {
        _ed = ed;
    }

    public IReadOnlyList<SelectedObjectRef> SelectMany(SelectionRequest request)
    {
        PromptSelectionOptions opts = new PromptSelectionOptions
        {
            MessageForAdding = request.Prompt
        };

        PromptSelectionResult result = _ed.GetSelection(opts);
        if (result.Status != PromptStatus.OK || result.Value == null)
            return Array.Empty<SelectedObjectRef>();

        var list = new List<SelectedObjectRef>(result.Value.Count);
        foreach (var id in result.Value.GetObjectIds())
        {
            string handle = id.Handle.ToString();
            list.Add(new SelectedObjectRef(handle));
        }

        return list;
    }
}

