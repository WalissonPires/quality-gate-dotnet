using Microsoft.Extensions.FileSystemGlobbing;

namespace QualityGate.Domain;

public static class GeneratedCodeFilter
{
    private static readonly string[] DefaultGeneratedSuffixes =
    [
        ".Designer.cs",
        "ModelSnapshot.cs",
        ".g.cs",
        ".g.i.cs"
    ];

    public static bool IsGeneratedOrIgnored(string filePath, IEnumerable<string>? customPatterns = null)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return false;
        }

        var normalizedPath = filePath.Replace('\\', '/').TrimStart('/');

        // 1. Check default suffixes (zero I/O, fast string ends-with check)
        foreach (var suffix in DefaultGeneratedSuffixes)
        {
            if (normalizedPath.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        // 2. Check custom glob patterns if provided
        if (customPatterns != null)
        {
            var patternsList = customPatterns.Where(p => !string.IsNullOrWhiteSpace(p)).ToList();
            if (patternsList.Count > 0)
            {
                var matcher = new Matcher(StringComparison.OrdinalIgnoreCase);
                foreach (var pattern in patternsList)
                {
                    matcher.AddInclude(pattern.Trim());
                }

                var matchResult = matcher.Match(normalizedPath);
                if (matchResult.HasMatches)
                {
                    return true;
                }
            }
        }

        return false;
    }

    public static IReadOnlyList<ChangedFile> FilterFiles(
        IEnumerable<ChangedFile> files,
        IEnumerable<string>? customPatterns = null)
    {
        ArgumentNullException.ThrowIfNull(files);

        var patterns = customPatterns?.ToList();
        return files
            .Where(f => !IsGeneratedOrIgnored(f.Path, patterns))
            .ToList()
            .AsReadOnly();
    }

    public static IReadOnlyList<string> FilterPaths(
        IEnumerable<string> paths,
        IEnumerable<string>? customPatterns = null)
    {
        ArgumentNullException.ThrowIfNull(paths);

        var patterns = customPatterns?.ToList();
        return paths
            .Where(p => !IsGeneratedOrIgnored(p, patterns))
            .ToList()
            .AsReadOnly();
    }
}
