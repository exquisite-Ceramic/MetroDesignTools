using System.Security.Cryptography;
using System.Text;
using MetroToolKits.Foundation.Core.Geometry;

namespace MetroToolKits.SectionGenerator.Core.Sections;

public sealed class SightLineGeometryHasher
{
    public const int CurrentHashVersion = 1;

    public string ComputeHash(IEnumerable<ElementSectionData> elements)
    {
        var sb = new StringBuilder();
        sb.Append("sightline-v");
        sb.Append(CurrentHashVersion);
        sb.Append('|');

        foreach (var element in elements
                     .Where(element => element.SightLines.Count > 0)
                     .OrderBy(GetPrimaryHandle, StringComparer.OrdinalIgnoreCase)
                     .ThenBy(element => element.ElementType, StringComparer.OrdinalIgnoreCase))
        {
            sb.Append(element.ElementType);
            sb.Append('|');
            sb.Append(GetPrimaryHandle(element));
            sb.Append('|');
            sb.Append(string.Join(",", element.SourceHandles.OrderBy(handle => handle, StringComparer.OrdinalIgnoreCase)));
            sb.Append('|');
            foreach (var line in element.SightLines.OrderBy(LineSignature, StringComparer.Ordinal))
            {
                AppendLine(sb, line);
                sb.Append(';');
            }

            sb.Append('#');
        }

        return ComputeSha256(sb.ToString());
    }

    private static string GetPrimaryHandle(ElementSectionData element)
        => !string.IsNullOrWhiteSpace(element.SourceHandle)
            ? element.SourceHandle
            : element.SourceHandles.FirstOrDefault() ?? string.Empty;

    private static string LineSignature(Line3D line)
        => $"{line.Start.X:F6},{line.Start.Y:F6},{line.Start.Z:F6}," +
           $"{line.End.X:F6},{line.End.Y:F6},{line.End.Z:F6}";

    private static void AppendLine(StringBuilder sb, Line3D line)
    {
        sb.Append(line.Start.X.ToString("F6"));
        sb.Append(',');
        sb.Append(line.Start.Y.ToString("F6"));
        sb.Append(',');
        sb.Append(line.Start.Z.ToString("F6"));
        sb.Append("->");
        sb.Append(line.End.X.ToString("F6"));
        sb.Append(',');
        sb.Append(line.End.Y.ToString("F6"));
        sb.Append(',');
        sb.Append(line.End.Z.ToString("F6"));
    }

    private static string ComputeSha256(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes).ToLowerInvariant()[..16];
    }
}
