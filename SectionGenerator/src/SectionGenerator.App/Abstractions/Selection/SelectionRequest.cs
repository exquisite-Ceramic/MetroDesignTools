namespace SectionGenerator.App.Abstractions.Selection;

using System.Collections.Generic;

public sealed record SelectionRequest(
    string Prompt,
    IReadOnlyCollection<string>? AllowedEntityTypeNames = null
);

