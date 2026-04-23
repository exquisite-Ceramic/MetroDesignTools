namespace MetroToolKits.Foundation.Building.Types;

/// <summary>
/// 楼板与墙体交接策略。
/// </summary>
public enum SlabWallJunctionMode
{
    StopAtWallFace,
    ContinueUnderWall,
    ReturnUpAtWallFace
}

/// <summary>
/// 楼层边界板配置。
/// </summary>
public sealed class BoundarySlabConfig
{
    public bool SlopeEnabled { get; set; }

    public double SlopeValue { get; set; }

    public string SlopeTarget { get; set; } = "StructuralSlab";
}
