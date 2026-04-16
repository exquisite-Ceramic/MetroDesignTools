using SectionGenerator.App.Abstractions.Selection;
using System.Collections.Generic;

namespace SectionGenerator.App.Abstractions;

public interface ISelectionService
{
    IReadOnlyList<SelectedObjectRef> SelectMany(SelectionRequest request);
}

