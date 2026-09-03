using QualityGate.Domain;
using QualityGate.Infrastructure.Roslyn;

namespace QualityGate.Gates;

public sealed class ComplexityGate : IQualityGate
{
    private readonly IComplexityAnalyzer _analyzer;

    public string Name => "Complexity";

    public ComplexityGate(IComplexityAnalyzer analyzer)
    {
        _analyzer = analyzer ?? throw new ArgumentNullException(nameof(analyzer));
    }

    public async Task<GateResult> ExecuteAsync(QualityContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var maxComplexityThreshold = context.Options.ChangedCode.MaxCyclomaticComplexity ?? 10;
        var maxMethodLinesThreshold = context.Options.ChangedCode.MaxMethodLines ?? 40;
        var maxClassLinesThreshold = context.Options.ChangedCode.MaxClassLines ?? 300;

        IEnumerable<string> sourceFiles;
        if (context.Target.Scope == QualityScope.Diff && context.ChangeSet != null)
        {
            sourceFiles = context.ChangeSet.CSharpSourceFiles.Select(f => f.Path);
        }
        else
        {
            var rootDir = Environment.CurrentDirectory;
            sourceFiles = Directory.GetFiles(rootDir, "*.cs", SearchOption.AllDirectories)
                .Select(f => Path.GetRelativePath(rootDir, f).Replace('\\', '/'))
                .Where(rel => !rel.StartsWith("bin/", StringComparison.OrdinalIgnoreCase) &&
                              !rel.Contains("/bin/", StringComparison.OrdinalIgnoreCase) &&
                              !rel.StartsWith("obj/", StringComparison.OrdinalIgnoreCase) &&
                              !rel.Contains("/obj/", StringComparison.OrdinalIgnoreCase))
                .ToList();
        }
        var ignorePatterns = (context.Options.ChangedCode.IgnorePatterns ?? [])
            .Concat(context.Options.IgnorePatterns ?? [])
            .Distinct();

        var fileList = GeneratedCodeFilter.FilterPaths(sourceFiles, ignorePatterns).ToList();

        if (context.Options.ChangedCode.IgnoreTestProjectsInComplexity && context.TestProjects.Count > 0)
        {
            fileList = fileList.Where(f => !IsTestFile(f, context.TestProjects)).ToList();
        }

        if (context.Verbose)
        {
            Console.WriteLine($"[Verbose] [Complexity] Analyzing {fileList.Count} C# source file(s) with Roslyn...");
        }
        var analysis = await _analyzer.AnalyzeFilesAsync(fileList, cancellationToken).ConfigureAwait(false);

        var findings = new List<GateFinding>();

        // Check method complexity
        foreach (var method in analysis.Methods)
        {
            if (method.CyclomaticComplexity > maxComplexityThreshold)
            {
                findings.Add(new GateFinding(
                    "CyclomaticComplexity",
                    $"Cyclomatic complexity is {method.CyclomaticComplexity} (threshold: {maxComplexityThreshold}).",
                    "Error",
                    method.FilePath,
                    $"{method.ClassName}.{method.MethodName}",
                    method.LineNumber,
                    actual: method.CyclomaticComplexity,
                    threshold: maxComplexityThreshold));
            }

            if (method.LineCount > maxMethodLinesThreshold)
            {
                findings.Add(new GateFinding(
                    "MethodLines",
                    $"Method length is {method.LineCount} lines (threshold: {maxMethodLinesThreshold}).",
                    "Error",
                    method.FilePath,
                    $"{method.ClassName}.{method.MethodName}",
                    method.LineNumber,
                    actual: method.LineCount,
                    threshold: maxMethodLinesThreshold));
            }
        }

        // Check class lines
        foreach (var cls in analysis.Classes)
        {
            if (cls.LineCount > maxClassLinesThreshold)
            {
                findings.Add(new GateFinding(
                    "ClassLines",
                    $"Class length is {cls.LineCount} lines (threshold: {maxClassLinesThreshold}).",
                    "Warning",
                    cls.FilePath,
                    cls.ClassName,
                    cls.LineNumber,
                    actual: cls.LineCount,
                    threshold: maxClassLinesThreshold));
            }
        }

        bool hasErrors = findings.Any(f => f.Severity.Equals("Error", StringComparison.OrdinalIgnoreCase));
        int maxObservedComplexity = analysis.MaxCyclomaticComplexity;

        if (hasErrors)
        {
            return GateResult.Fail(
                Name,
                $"Cyclomatic complexity or method length exceeds threshold in evaluated code.",
                TimeSpan.Zero,
                findings,
                actual: maxObservedComplexity,
                threshold: maxComplexityThreshold);
        }

        return GateResult.Pass(
            Name,
            $"Max cyclomatic complexity is {maxObservedComplexity} (threshold: {maxComplexityThreshold}).",
            TimeSpan.Zero,
            findings,
            actual: maxObservedComplexity,
            threshold: maxComplexityThreshold);
    }

    private static bool IsTestFile(string filePath, IEnumerable<string> testProjects)
    {
        if (string.IsNullOrWhiteSpace(filePath) || testProjects == null)
        {
            return false;
        }

        var normalizedFile = filePath.Replace('\\', '/').TrimStart('/');

        foreach (var testProject in testProjects)
        {
            var normalizedProj = testProject.Replace('\\', '/').TrimStart('/');
            var projDir = Path.GetDirectoryName(normalizedProj)?.Replace('\\', '/').TrimEnd('/');
            if (string.IsNullOrEmpty(projDir))
            {
                continue;
            }

            if (normalizedFile.StartsWith(projDir + "/", StringComparison.OrdinalIgnoreCase) ||
                normalizedFile.Equals(projDir, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
