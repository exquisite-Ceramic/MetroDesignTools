using System.Security.Cryptography;
using System.Text;

namespace MetroToolKits.Bootstrap;

/// <summary>
/// 解析运行期可写目录，避免把状态写回安装目录。
/// </summary>
public static class RuntimePathResolver
{
    public static string GetPackageScopedUserPath(string assemblyLocation, params string[] segments)
    {
        var path = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MetroToolKits",
            "Packages",
            BuildPackageScope(assemblyLocation));

        foreach (var segment in segments.Where(static s => !string.IsNullOrWhiteSpace(s)))
        {
            path = Path.Combine(path, segment);
        }

        return path;
    }

    private static string BuildPackageScope(string assemblyLocation)
    {
        var assemblyDirectory = Path.GetDirectoryName(assemblyLocation)
            ?? AppDomain.CurrentDomain.BaseDirectory;
        var normalized = Path.GetFullPath(assemblyDirectory)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .ToUpperInvariant();

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(normalized));
        return $"pkg-{Convert.ToHexString(bytes)[..12]}";
    }
}
