namespace MetroToolKits.Foundation.Core.Geometry;

/// <summary>
/// 构件生成剖面几何时使用的统一上下文。
/// </summary>
public sealed class SectionGeometryContext
{
    public required Line3D SectionLine { get; init; }

    public required Vector3D ViewDirection { get; init; }

    public required SectionCoordinateProjector Projector { get; init; }

    public FloorVerticalProfile? VerticalProfile { get; init; }
}
