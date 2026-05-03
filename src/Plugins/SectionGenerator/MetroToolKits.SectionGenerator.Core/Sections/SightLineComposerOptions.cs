namespace MetroToolKits.SectionGenerator.Core.Sections;

public sealed class SightLineComposerOptions
{
    public bool Enabled { get; init; } = true;

    public bool IncludeSlabs { get; init; }

    public bool IncludeWalls { get; init; } = true;

    public bool IncludeColumns { get; init; } = true;
}
