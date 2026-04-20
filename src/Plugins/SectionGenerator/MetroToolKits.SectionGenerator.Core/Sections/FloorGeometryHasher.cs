using System.Security.Cryptography;
using System.Text;
using MetroToolKits.Foundation.Building.Elements;

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
    public string ComputeHash(IEnumerable<BuildingElement> elements)
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

            _ => element.Id.ToString()
        };
    }

    private static string ComputeSha256(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes).ToLowerInvariant()[..16]; // 取前16位
    }
}
