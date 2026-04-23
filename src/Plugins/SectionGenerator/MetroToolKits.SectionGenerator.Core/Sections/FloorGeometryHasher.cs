using System.Security.Cryptography;
using System.Text;
using MetroToolKits.Foundation.Building.Elements;
using MetroToolKits.Foundation.Core.Geometry;

namespace MetroToolKits.SectionGenerator.Core.Sections;

/// <summary>
/// 楼层几何指纹计算器
/// 相同构件列表产生相同哈希；构件增删改导致哈希变化
/// </summary>
public sealed class FloorGeometryHasher
{
    /// <summary>
    /// 计算构件列表的几何哈希
    /// </summary>
    public string ComputeHash(
        IEnumerable<BuildingElement> elements,
        FloorConfig? floorConfig = null,
        FloorVerticalProfile? verticalProfile = null)
    {
        var sb = new StringBuilder();

        // 按 SourceHandle 排序保证确定性
        foreach (var e in elements.OrderBy(e => e.SourceHandle ?? e.Id.ToString()))
        {
            sb.Append(e.ElementType);
            sb.Append('|');
            sb.Append(ComputeElementHash(e));
            sb.Append(';');
        }

        if (floorConfig != null)
        {
            sb.Append("|floor:");
            sb.Append(floorConfig.Name);
            sb.Append('|');
            sb.Append(floorConfig.Height.ToString("F2"));
            sb.Append('|');
            sb.Append(floorConfig.TopBoundarySlab.TemplateId);
            sb.Append('|');
            sb.Append(floorConfig.TopBoundarySlab.SlopeEnabled);
            sb.Append('|');
            sb.Append(floorConfig.TopBoundarySlab.SlopeValue.ToString("F6"));
            sb.Append('|');
            sb.Append(floorConfig.BottomBoundarySlab.TemplateId);
            sb.Append('|');
            sb.Append(floorConfig.BottomBoundarySlab.SlopeEnabled);
            sb.Append('|');
            sb.Append(floorConfig.BottomBoundarySlab.SlopeValue.ToString("F6"));
            sb.Append('|');
            sb.Append(floorConfig.FinishThickness.ToString("F2"));
        }

        if (verticalProfile != null)
        {
            sb.Append("|profile:");
            sb.Append(verticalProfile.BottomStructuralBottom.StartY.ToString("F2"));
            sb.Append(',');
            sb.Append(verticalProfile.BottomStructuralBottom.EndY.ToString("F2"));
            sb.Append('|');
            sb.Append(verticalProfile.BottomStructuralTop.StartY.ToString("F2"));
            sb.Append(',');
            sb.Append(verticalProfile.BottomStructuralTop.EndY.ToString("F2"));
            sb.Append('|');
            sb.Append(verticalProfile.BottomBoundaryBottom.StartY.ToString("F2"));
            sb.Append(',');
            sb.Append(verticalProfile.BottomBoundaryBottom.EndY.ToString("F2"));
            sb.Append('|');
            sb.Append(verticalProfile.BottomBoundaryTop.StartY.ToString("F2"));
            sb.Append(',');
            sb.Append(verticalProfile.BottomBoundaryTop.EndY.ToString("F2"));
            sb.Append('|');
            sb.Append(verticalProfile.TopStructuralBottom.StartY.ToString("F2"));
            sb.Append(',');
            sb.Append(verticalProfile.TopStructuralBottom.EndY.ToString("F2"));
            sb.Append('|');
            sb.Append(verticalProfile.TopStructuralTop.StartY.ToString("F2"));
            sb.Append(',');
            sb.Append(verticalProfile.TopStructuralTop.EndY.ToString("F2"));
            sb.Append('|');
            sb.Append(verticalProfile.TopBoundaryBottom.StartY.ToString("F2"));
            sb.Append(',');
            sb.Append(verticalProfile.TopBoundaryBottom.EndY.ToString("F2"));
            sb.Append('|');
            sb.Append(verticalProfile.TopBoundaryTop.StartY.ToString("F2"));
            sb.Append(',');
            sb.Append(verticalProfile.TopBoundaryTop.EndY.ToString("F2"));
        }

        return ComputeSha256(sb.ToString());
    }

    private string ComputeElementHash(BuildingElement element)
    {
        return element switch
        {
            Wall w => $"{w.StartPoint.X:F2},{w.StartPoint.Y:F2},{w.StartPoint.Z:F2}" +
                      $"|{w.EndPoint.X:F2},{w.EndPoint.Y:F2},{w.EndPoint.Z:F2}" +
                      $"|{w.Height:F2}|{w.Thickness:F2}|{w.BaseElevation:F2}",

            Slab s => string.Join(",", s.Outline.Select(p => $"{p.X:F2},{p.Y:F2},{p.Z:F2}")) +
                      $"|{s.Thickness:F2}|{s.TopElevation:F2}|{s.SlopeValue:F4}",

            Column c => $"{c.CenterPoint.X:F2},{c.CenterPoint.Y:F2},{c.CenterPoint.Z:F2}" +
                        $"|{c.Width:F2}|{c.Depth:F2}|{c.Height:F2}|{c.Rotation:F4}",

            CompositeWallElement compositeWall =>
                $"{compositeWall.TemplateId}|{compositeWall.CoreSegment.StartPoint.X:F2},{compositeWall.CoreSegment.StartPoint.Y:F2}" +
                $"|{compositeWall.CoreSegment.EndPoint.X:F2},{compositeWall.CoreSegment.EndPoint.Y:F2}" +
                $"|{compositeWall.CoreSegment.Thickness:F2}|{compositeWall.CoreSegment.Height:F2}" +
                $"|{compositeWall.VerticalAnchorMode}" +
                $"|{string.Join(",", compositeWall.SourceHandles.OrderBy(handle => handle, StringComparer.OrdinalIgnoreCase))}" +
                $"|{string.Join(";", compositeWall.LayerSections.Select(layer =>
                    $"{layer.Name}:{layer.MaterialOrCategory}:{layer.IsCore}:{layer.InnerOffset:F2}:{layer.OuterOffset:F2}:{layer.VisibleInSection}"))}",

            CompositeSlabElement compositeSlab =>
                $"{compositeSlab.TemplateId}|{string.Join(",", compositeSlab.CoreArea.Outline.Select(p => $"{p.X:F2},{p.Y:F2},{p.Z:F2}"))}" +
                $"|{compositeSlab.WallJunctionMode}" +
                $"|{string.Join(",", compositeSlab.SourceHandles.OrderBy(handle => handle, StringComparer.OrdinalIgnoreCase))}" +
                $"|{string.Join(";", compositeSlab.LayerSections.Select(layer =>
                    $"{layer.Name}:{layer.MaterialOrCategory}:{layer.Side}:{layer.IsCore}:{layer.TopOffset:F2}:{layer.BottomOffset:F2}:{layer.VisibleInSection}"))}",

            _ => element.Id.ToString()
        };
    }

    private static string ComputeSha256(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes).ToLowerInvariant()[..16]; // 取前16位
    }
}
