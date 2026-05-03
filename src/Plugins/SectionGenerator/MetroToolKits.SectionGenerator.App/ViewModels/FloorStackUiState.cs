namespace MetroToolKits.SectionGenerator.App.ViewModels;

public sealed class FloorStackUiState
{
    public FloorStackUiRole Role { get; init; }

    public string RoleText { get; init; } = string.Empty;

    public string StackPolicyText { get; init; } = string.Empty;

    public string RoleDescription { get; init; } = string.Empty;

    public bool IsBottomBoundaryEffective { get; init; }

    public bool IsTopBoundaryEffective { get; init; }

    public string BottomBoundaryLabelText { get; init; } = string.Empty;

    public string TopBoundaryLabelText { get; init; } = string.Empty;

    public string BottomThicknessLabelText { get; init; } = string.Empty;

    public string TopThicknessLabelText { get; init; } = string.Empty;

    public string BottomSlopeLabelText { get; init; } = string.Empty;

    public string TopSlopeLabelText { get; init; } = string.Empty;

    public string BottomBoundaryHint { get; init; } = string.Empty;

    public string TopBoundaryHint { get; init; } = string.Empty;
}
