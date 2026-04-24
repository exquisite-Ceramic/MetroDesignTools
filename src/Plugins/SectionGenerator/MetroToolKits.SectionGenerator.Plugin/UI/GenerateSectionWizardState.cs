using MetroToolKits.Foundation.Core.Geometry;
using MetroToolKits.SectionGenerator.App.Models;
using MetroToolKits.SectionGenerator.App.UseCases;

namespace MetroToolKits.SectionGenerator.Plugin.UI;

public enum GenerateSectionWizardAction
{
    None,
    OpenFloorConfig,
    OpenLayerMapping,
    ViewUpdateDetails,
    PickCutLine,
    PickLocalScope,
    PickInsertionPoint,
    Generate
}

public sealed class GenerateSectionWizardState
{
    public int CurrentStep { get; set; }

    public double ViewDepth { get; set; } = 3000;

    public bool GenerateSingleFloor { get; set; }

    public string? TargetFloorName { get; set; }

    public bool UseLocalScope { get; set; }

    public string? CutLineHandle { get; set; }

    public Point3D CutLineStart { get; set; }

    public Point3D CutLineEnd { get; set; }

    public ScopeBounds2D? LocalScopeBounds { get; set; }

    public Point3D InsertionPoint { get; set; }

    public bool HasInsertionPoint { get; set; }

    public GenerateSectionPreflightResult PreflightResult { get; set; } = new();

    public bool HasCutLine => !string.IsNullOrWhiteSpace(CutLineHandle);

    public bool CanGenerate =>
        PreflightResult.CanGenerate &&
        HasCutLine &&
        HasInsertionPoint &&
        (!UseLocalScope || LocalScopeBounds.HasValue) &&
        (!GenerateSingleFloor || !string.IsNullOrWhiteSpace(TargetFloorName));

    public string? ResolveTargetFloorName()
        => GenerateSingleFloor ? TargetFloorName : null;

    public string? ResolveLocalScopeFloorName()
    {
        if (!UseLocalScope)
        {
            return null;
        }

        if (GenerateSingleFloor)
        {
            return TargetFloorName;
        }

        return PreflightResult.ConfigDocument.Config.AlignmentBaseFloorName;
    }

    public void Normalize()
    {
        var floors = PreflightResult.ConfigDocument.Config.Floors;
        if (floors.Count <= 1)
        {
            GenerateSingleFloor = false;
            TargetFloorName = floors.FirstOrDefault()?.Name;
            return;
        }

        if (GenerateSingleFloor)
        {
            var hasSelectedFloor = floors.Any(floor =>
                string.Equals(floor.Name, TargetFloorName, StringComparison.OrdinalIgnoreCase));
            if (!hasSelectedFloor)
            {
                TargetFloorName = floors.FirstOrDefault()?.Name;
            }
        }
        else
        {
            TargetFloorName = null;
        }
    }
}
