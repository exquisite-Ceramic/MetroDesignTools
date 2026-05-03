namespace MetroToolKits.SectionGenerator.App.ViewModels;

public static class FloorStackUiStateBuilder
{
    private const string StackPolicyText = "共享层间板";
    private const string SuppressedBottomBoundaryHint =
        "当前堆叠策略为“共享层间板”，本层下边界由上一层上部层间板表达，本层不单独生成底板线、填充或高度贡献。";

    public static FloorStackUiState Create(int floorIndex, int floorCount)
    {
        // This mirrors FloorStackBoundaryPolicy.ShareInteriorBoundaries UI semantics.
        if (floorCount <= 1)
        {
            return new FloorStackUiState
            {
                Role = FloorStackUiRole.Single,
                RoleText = "单层",
                StackPolicyText = StackPolicyText,
                RoleDescription = "底板和顶板均会生成。",
                IsBottomBoundaryEffective = true,
                IsTopBoundaryEffective = true,
                BottomBoundaryLabelText = "底边界板模板",
                TopBoundaryLabelText = "顶边界板模板",
                BottomThicknessLabelText = "底板厚度 (mm):",
                TopThicknessLabelText = "顶板厚度 (mm):",
                BottomSlopeLabelText = "底板坡度 (%):",
                TopSlopeLabelText = "顶板坡度 (%):",
                BottomBoundaryHint = "底边界板会生成线、填充并参与高度贡献。",
                TopBoundaryHint = "顶边界板会生成线、填充并参与高度贡献。"
            };
        }

        if (floorIndex < 0 || floorIndex >= floorCount)
        {
            throw new ArgumentOutOfRangeException(nameof(floorIndex), "楼层索引必须位于楼层数量范围内。");
        }

        if (floorIndex == 0)
        {
            return new FloorStackUiState
            {
                Role = FloorStackUiRole.Bottom,
                RoleText = "底层",
                StackPolicyText = StackPolicyText,
                RoleDescription = "底层底板和上部层间板均会生成。",
                IsBottomBoundaryEffective = true,
                IsTopBoundaryEffective = true,
                BottomBoundaryLabelText = "底层底板模板",
                TopBoundaryLabelText = "上部层间板模板",
                BottomThicknessLabelText = "底层底板厚度 (mm):",
                TopThicknessLabelText = "上部层间板厚度 (mm):",
                BottomSlopeLabelText = "底层底板坡度 (%):",
                TopSlopeLabelText = "上部层间板坡度 (%):",
                BottomBoundaryHint = "底层底板会生成线、填充并参与高度贡献。",
                TopBoundaryHint = "本层上部层间板会生成线、填充并参与高度贡献。"
            };
        }

        if (floorIndex == floorCount - 1)
        {
            return new FloorStackUiState
            {
                Role = FloorStackUiRole.Top,
                RoleText = "顶层",
                StackPolicyText = StackPolicyText,
                RoleDescription = "本层下边界由上一层上部层间板表达，屋面/顶板会生成。",
                IsBottomBoundaryEffective = false,
                IsTopBoundaryEffective = true,
                BottomBoundaryLabelText = "下边界板",
                TopBoundaryLabelText = "屋面/顶板模板",
                BottomThicknessLabelText = "下边界板厚度 (当前不参与生成):",
                TopThicknessLabelText = "屋面/顶板厚度 (mm):",
                BottomSlopeLabelText = "下边界板坡度 (当前不参与生成):",
                TopSlopeLabelText = "屋面/顶板坡度 (%):",
                BottomBoundaryHint = SuppressedBottomBoundaryHint,
                TopBoundaryHint = "屋面/顶板会生成线、填充并参与高度贡献。"
            };
        }

        return new FloorStackUiState
        {
            Role = FloorStackUiRole.Middle,
            RoleText = "中间层",
            StackPolicyText = StackPolicyText,
            RoleDescription = "本层下边界由上一层上部层间板表达，上部层间板会生成。",
            IsBottomBoundaryEffective = false,
            IsTopBoundaryEffective = true,
            BottomBoundaryLabelText = "下边界板",
            TopBoundaryLabelText = "上部层间板模板",
            BottomThicknessLabelText = "下边界板厚度 (当前不参与生成):",
            TopThicknessLabelText = "上部层间板厚度 (mm):",
            BottomSlopeLabelText = "下边界板坡度 (当前不参与生成):",
            TopSlopeLabelText = "上部层间板坡度 (%):",
            BottomBoundaryHint = SuppressedBottomBoundaryHint,
            TopBoundaryHint = "本层上部层间板会生成线、填充并参与高度贡献。"
        };
    }
}
