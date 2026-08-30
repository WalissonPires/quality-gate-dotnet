using System.Xml.Linq;
using QualityGate.Domain;

namespace QualityGate.Application;

public sealed class ProjectDiscovery
{
    private readonly string _rootDirectory;

    public ProjectDiscovery(string rootDirectory)
    {
        if (string.IsNullOrWhiteSpace(rootDirectory))
        {
            throw new ArgumentException("Root directory cannot be null or whitespace.", nameof(rootDirectory));
        }

        _rootDirectory = Path.GetFullPath(rootDirectory);
    }

    public IReadOnlyList<string> FindAllProjects()
    {
        if (!Directory.Exists(_rootDirectory))
        {
            return [];
        }

        return Directory.GetFiles(_rootDirectory, "*.csproj", SearchOption.AllDirectories)
            .Select(p => new { FullPath = p, Relative = Path.GetRelativePath(_rootDirectory, p).Replace('\\', '/') })
            .Where(p => !p.Relative.StartsWith("bin/", StringComparison.OrdinalIgnoreCase) &&
                        !p.Relative.Contains("/bin/", StringComparison.OrdinalIgnoreCase) &&
                        !p.Relative.StartsWith("obj/", StringComparison.OrdinalIgnoreCase) &&
                        !p.Relative.Contains("/obj/", StringComparison.OrdinalIgnoreCase))
            .Select(p => p.Relative)
            .ToList();
    }

    public bool IsTestProject(string projectPath)
    {
        var fullPath = Path.IsPathRooted(projectPath) ? projectPath : Path.Combine(_rootDirectory, projectPath);
        if (!File.Exists(fullPath)) return false;

        try
        {
            var doc = XDocument.Load(fullPath);
            var isTestElement = doc.Descendants("IsTestProject").FirstOrDefault();
            if (isTestElement != null && bool.TryParse(isTestElement.Value, out var isTest))
            {
                return isTest;
            }

            var packageRefs = doc.Descendants("PackageReference")
                .Select(p => p.Attribute("Include")?.Value ?? string.Empty)
                .ToList();

            if (packageRefs.Any(p => p.Contains("xunit", StringComparison.OrdinalIgnoreCase) ||
                                     p.Contains("nunit", StringComparison.OrdinalIgnoreCase) ||
                                     p.Contains("Microsoft.NET.Test.Sdk", StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }

            var projectName = Path.GetFileNameWithoutExtension(projectPath);
            return projectName.EndsWith(".Tests", StringComparison.OrdinalIgnoreCase) ||
                   projectName.EndsWith(".Test", StringComparison.OrdinalIgnoreCase) ||
                   projectName.EndsWith("Tests", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    public IReadOnlyList<string> GetProjectReferences(string projectPath)
    {
        var fullPath = Path.IsPathRooted(projectPath) ? projectPath : Path.Combine(_rootDirectory, projectPath);
        if (!File.Exists(fullPath)) return [];

        try
        {
            var doc = XDocument.Load(fullPath);
            var projectDir = Path.GetDirectoryName(fullPath)!;

            return doc.Descendants("ProjectReference")
                .Select(p => p.Attribute("Include")?.Value)
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Select(refPath =>
                {
                    var resolved = Path.GetFullPath(Path.Combine(projectDir, refPath!));
                    return GetRelativePath(resolved);
                })
                .ToList();
        }
        catch
        {
            return [];
        }
    }

    public string? FindOwningProject(string filePath, IEnumerable<string> allProjects)
    {
        var normalizedFile = filePath.Replace('\\', '/');
        var candidateProjects = allProjects
            .Select(p => new
            {
                ProjectPath = p,
                Dir = (Path.GetDirectoryName(p) ?? string.Empty).Replace('\\', '/')
            })
            .Where(p => !string.IsNullOrEmpty(p.Dir) && (normalizedFile.StartsWith(p.Dir + "/", StringComparison.OrdinalIgnoreCase) || normalizedFile.Equals(p.ProjectPath, StringComparison.OrdinalIgnoreCase)))
            .OrderByDescending(p => p.Dir.Length)
            .ToList();

        return candidateProjects.FirstOrDefault()?.ProjectPath;
    }

    public IReadOnlyList<string> FindAffectedProjects(ChangeSet changeSet, IEnumerable<string> allProjects)
    {
        var projectList = allProjects.ToList();
        var affected = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var file in changeSet.NonDeletedFiles)
        {
            var owning = FindOwningProject(file.Path, projectList);
            if (!string.IsNullOrWhiteSpace(owning))
            {
                affected.Add(owning);
            }
        }

        return affected.ToList();
    }

    public IReadOnlyList<string> FindTestProjectsFor(IEnumerable<string> affectedProjects, IEnumerable<string> allProjects)
    {
        var projectList = allProjects.ToList();
        var affectedSet = new HashSet<string>(affectedProjects.Select(NormalizePath), StringComparer.OrdinalIgnoreCase);
        var testProjects = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var project in projectList)
        {
            if (!IsTestProject(project)) continue;

            // Check if the test project itself is affected
            if (affectedSet.Contains(NormalizePath(project)))
            {
                testProjects.Add(project);
                continue;
            }

            // Check if test project references any affected project
            var refs = GetProjectReferences(project).Select(NormalizePath);
            if (refs.Any(r => affectedSet.Contains(r)))
            {
                testProjects.Add(project);
            }
        }

        // If no test projects directly match references, but there are test projects in the repo, include test projects that might test the system
        if (testProjects.Count == 0)
        {
            foreach (var p in projectList.Where(IsTestProject))
            {
                testProjects.Add(p);
            }
        }

        return testProjects.ToList();
    }

    public IReadOnlyList<string> FindSourceFilesForNamespace(string @namespace)
    {
        if (string.IsNullOrWhiteSpace(@namespace) || !Directory.Exists(_rootDirectory))
        {
            return [];
        }

        var namespaceParts = @namespace.Split('.', StringSplitOptions.RemoveEmptyEntries);
        var csFiles = Directory.GetFiles(_rootDirectory, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}") &&
                        !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") &&
                        !f.Contains($"{Path.AltDirectorySeparatorChar}bin{Path.AltDirectorySeparatorChar}") &&
                        !f.Contains($"{Path.AltDirectorySeparatorChar}obj{Path.AltDirectorySeparatorChar}"))
            .ToList();

        var matchedFiles = new List<string>();
        foreach (var file in csFiles)
        {
            var relative = GetRelativePath(file);
            // Check folder path match or namespace declaration in file
            var folderMatch = namespaceParts.All(p => relative.Contains(p, StringComparison.OrdinalIgnoreCase));
            if (folderMatch)
            {
                matchedFiles.Add(relative);
            }
        }

        return matchedFiles;
    }

    public IReadOnlyList<string> DiscoverFeatures()
    {
        var featuresDir = Path.Combine(_rootDirectory, "backend", "Features");
        if (!Directory.Exists(featuresDir))
        {
            featuresDir = Path.Combine(_rootDirectory, "Features");
        }

        if (!Directory.Exists(featuresDir))
        {
            return [];
        }

        return Directory.GetDirectories(featuresDir)
            .Select(Path.GetFileName)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Cast<string>()
            .OrderBy(n => n)
            .ToList();
    }

    public static string? FindFeatureForFile(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath)) return null;

        var normalized = filePath.Replace('\\', '/');
        var match = System.Text.RegularExpressions.Regex.Match(
            normalized,
            @"(?:backend/)?Features/([^/]+)/",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        return match.Success ? match.Groups[1].Value : null;
    }

    private string GetRelativePath(string fullPath)
    {
        return Path.GetRelativePath(_rootDirectory, fullPath).Replace('\\', '/');
    }

    private static string NormalizePath(string path) => path.Replace('\\', '/');
}
