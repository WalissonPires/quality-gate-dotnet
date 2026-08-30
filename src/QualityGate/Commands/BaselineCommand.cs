using System.CommandLine;
using System.Reflection;
using QualityGate.Application;
using QualityGate.Configuration;
using QualityGate.Domain;
using QualityGate.Infrastructure.Architecture;
using QualityGate.Infrastructure.Coverage;
using QualityGate.Infrastructure.Dotnet;
using QualityGate.Infrastructure.Git;
using QualityGate.Infrastructure.Process;
using QualityGate.Infrastructure.Roslyn;

namespace QualityGate.Commands;

public static class BaselineCommand
{
    public static Command Create(
        IProcessRunner? processRunner = null,
        IGitService? gitService = null,
        IDotnetService? dotnetService = null,
        ICoverageParser? coverageParser = null,
        IArchitectureValidator? architectureValidator = null,
        IComplexityAnalyzer? complexityAnalyzer = null)
    {
        var baselineCommand = new Command("baseline", "Manage quality baselines for ratchet verification");
        var recordCommand = new Command("record", "Measure and record current repository quality metrics to baseline file");

        var outputOption = new Option<string>("--output", () => ".qualitygate/baseline.json", "Output path for baseline JSON file");
        var projectOption = new Option<string?>("--project", "Evaluate and record baseline for a specific .csproj project");
        var configOption = new Option<string>("--config", () => "qualitygate.json", "Path to configuration file");
        var verboseOption = new Option<bool>("--verbose", "Enable detailed diagnostic output");

        recordCommand.AddOption(outputOption);
        recordCommand.AddOption(projectOption);
        recordCommand.AddOption(configOption);
        recordCommand.AddOption(verboseOption);

        recordCommand.SetHandler(async (context) =>
        {
            var output = context.ParseResult.GetValueForOption(outputOption) ?? ".qualitygate/baseline.json";
            var project = context.ParseResult.GetValueForOption(projectOption);
            var config = context.ParseResult.GetValueForOption(configOption) ?? "qualitygate.json";
            var verbose = context.ParseResult.GetValueForOption(verboseOption);

            var exitCode = await RecordAsync(
                output, config, verbose,
                project,
                null,
                processRunner, gitService, dotnetService, coverageParser, architectureValidator, complexityAnalyzer,
                context.GetCancellationToken()).ConfigureAwait(false);

            context.ExitCode = exitCode;
        });

        baselineCommand.AddCommand(recordCommand);
        return baselineCommand;
    }

    public static async Task<int> RecordAsync(
        string outputPath,
        string configPath,
        bool verbose,
        string? project = null,
        string? workingDirectory = null,
        IProcessRunner? processRunner = null,
        IGitService? gitService = null,
        IDotnetService? dotnetService = null,
        ICoverageParser? coverageParser = null,
        IArchitectureValidator? architectureValidator = null,
        IComplexityAnalyzer? complexityAnalyzer = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var workingDir = FindRepositoryRoot(string.IsNullOrWhiteSpace(workingDirectory) ? Environment.CurrentDirectory : workingDirectory);
            processRunner ??= new ProcessRunner();
            gitService ??= new GitService(processRunner);
            dotnetService ??= new DotnetService(processRunner);
            coverageParser ??= new CoberturaParser();
            architectureValidator ??= new ArchitectureValidator();
            complexityAnalyzer ??= new ComplexityAnalyzer();

            string headCommit = "unknown";
            try
            {
                if (await gitService.IsGitRepositoryAsync(workingDir, cancellationToken).ConfigureAwait(false))
                {
                    headCommit = await gitService.GetHeadCommitAsync(workingDir, cancellationToken).ConfigureAwait(false);
                }
            }
            catch
            {
                // Fallback if git HEAD resolution fails
            }

            // 2. Load Configuration
            var resolvedConfigPath = Path.IsPathRooted(configPath) ? configPath : Path.Combine(workingDir, configPath);
            QualityGateOptions options;
            if (File.Exists(resolvedConfigPath))
            {
                options = await ConfigurationLoader.LoadAsync(resolvedConfigPath, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                var dotQualityGateConfig = Path.Combine(workingDir, ".qualitygate", "qualitygate.json");
                options = File.Exists(dotQualityGateConfig)
                    ? await ConfigurationLoader.LoadAsync(dotQualityGateConfig, cancellationToken).ConfigureAwait(false)
                    : new QualityGateOptions();
            }

            // 3. Discover Projects and Features
            var discovery = new ProjectDiscovery(workingDir);
            var allProjects = discovery.FindAllProjects();
            var sourceProjects = allProjects.Where(p => !discovery.IsTestProject(p)).ToList();
            var testProjects = allProjects.Where(discovery.IsTestProject).ToList();
            if (!string.IsNullOrWhiteSpace(project))
            {
                sourceProjects = [project];
                testProjects = discovery.FindTestProjectsFor([project], allProjects).ToList();
            }

            var discoveredFeatures = discovery.DiscoverFeatures();
            if (verbose)
            {
                Console.WriteLine($"[Verbose] Discovered {sourceProjects.Count} source projects, {testProjects.Count} test projects, {discoveredFeatures.Count} features.");
            }

            // 4. Build and Collect Warnings
            int totalWarnings = 0;
            foreach (var proj in sourceProjects)
            {
                var buildRes = await dotnetService.BuildAsync(proj, "Release", TimeSpan.FromMinutes(3), cancellationToken).ConfigureAwait(false);
                var warnings = Gates.StaticAnalysisGate.ParseWarnings(buildRes.StandardOutput + "\n" + buildRes.StandardError);
                totalWarnings += warnings.Count;
            }

            // 5. Run Tests and Collect Coverage
            var tempArtifactDir = Path.Combine(workingDir, ".qualitygate", "artifacts", $"baseline-{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempArtifactDir);

            int totalTests = 0;
            int failedTests = 0;

            foreach (var testProj in testProjects)
            {
                var resultsDir = Path.Combine(tempArtifactDir, "TestResults", Path.GetFileNameWithoutExtension(testProj));
                var testRes = await dotnetService.TestAsync(testProj, resultsDir, collectCoverage: true, configuration: "Release", noBuild: false, timeout: TimeSpan.FromMinutes(5), cancellationToken: cancellationToken).ConfigureAwait(false);
                totalTests += testRes.TotalTests;
                failedTests += testRes.FailedTests;
                if (verbose)
                {
                    Console.WriteLine($"[Verbose] Test project: {testProj}");
                    Console.WriteLine($"[Verbose]   Results dir : {resultsDir}");
                    Console.WriteLine($"[Verbose]   Tests       : total={testRes.TotalTests} passed={testRes.PassedTests} failed={testRes.FailedTests}");
                    if (testRes.CoverageReportPath is not null)
                        Console.WriteLine($"[Verbose]   Coverage XML: {testRes.CoverageReportPath}");
                    else
                        Console.WriteLine($"[Verbose]   Coverage XML: not found");
                }
            }

            var coverageFiles = Directory.Exists(tempArtifactDir)
                ? Directory.GetFiles(tempArtifactDir, "coverage.cobertura.xml", SearchOption.AllDirectories)
                : [];
            var coverageSummary = await coverageParser.ParseMultipleAsync(coverageFiles, cancellationToken).ConfigureAwait(false);
            if (verbose)
            {
                Console.WriteLine($"[Verbose] Coverage files found: {coverageFiles.Length}");
                foreach (var f in coverageFiles) Console.WriteLine($"[Verbose]   {f}");
                Console.WriteLine($"[Verbose] Coverage summary: lines-valid={coverageSummary.TotalLines} lines-covered={coverageSummary.CoveredLines} branches-valid={coverageSummary.TotalBranches} branches-covered={coverageSummary.CoveredBranches}");
            }

            // Sanity guard: refuse to record baseline when coverage is provably untrustworthy
            if (failedTests == 0 && coverageSummary.TotalLines > 0 && coverageSummary.CoveredLines == 0)
            {
                Console.Error.WriteLine("Coverage collection produced zero covered lines with passing tests. Baseline aborted because the artifact is not trustworthy.");
                // Preserve artifacts for diagnosis instead of deleting them
                Console.Error.WriteLine($"Artifacts preserved for diagnosis at: {tempArtifactDir}");
                return 3;
            }
            var allCsFiles = Directory.GetFiles(workingDir, "*.cs", SearchOption.AllDirectories)
                .Select(f => Path.GetRelativePath(workingDir, f).Replace('\\', '/'))
                .Where(rel => !rel.StartsWith("bin/", StringComparison.OrdinalIgnoreCase) &&
                              !rel.Contains("/bin/", StringComparison.OrdinalIgnoreCase) &&
                              !rel.StartsWith("obj/", StringComparison.OrdinalIgnoreCase) &&
                              !rel.Contains("/obj/", StringComparison.OrdinalIgnoreCase))
                .ToList();

            var architectureViolations = await architectureValidator.ValidateAsync(allCsFiles, options.Architecture.Rules, cancellationToken).ConfigureAwait(false);

            // 7. Complexity Analysis
            var complexityAnalysis = await complexityAnalyzer.AnalyzeFilesAsync(allCsFiles, cancellationToken).ConfigureAwait(false);

            // 8. Synthesize Global and Feature Metrics
            var globalMetrics = new GlobalMetrics(
                LineCoverage: Math.Round(coverageSummary.LineRate, 1),
                BranchCoverage: Math.Round(coverageSummary.BranchRate, 1),
                TotalTests: totalTests,
                FailedTests: failedTests,
                TotalWarnings: totalWarnings,
                ArchitectureViolations: architectureViolations.Count,
                MaxCyclomaticComplexity: complexityAnalysis.MaxCyclomaticComplexity);

            var featureMetricsDict = new Dictionary<string, FeatureMetrics>(StringComparer.OrdinalIgnoreCase);
            foreach (var feature in discoveredFeatures)
            {
                // Feature coverage
                var featureReports = coverageSummary.FileReports
                    .Where(f => ProjectDiscovery.FindFeatureForFile(f.Key)?.Equals(feature, StringComparison.OrdinalIgnoreCase) == true)
                    .Select(f => f.Value)
                    .ToList();

                decimal? featureLineCoverage = null;
                decimal? featureBranchCoverage = null;
                int featureTotalLines = 0;

                if (featureReports.Count > 0)
                {
                    int fValid = featureReports.Sum(r => r.LinesValid);
                    int fCovered = featureReports.Sum(r => r.LinesCovered);
                    int fbValid = featureReports.Sum(r => r.BranchesValid);
                    int fbCovered = featureReports.Sum(r => r.BranchesCovered);
                    featureTotalLines = fValid;

                    if (fValid > 0) featureLineCoverage = Math.Round((decimal)fCovered / fValid * 100m, 1);
                    if (fbValid > 0) featureBranchCoverage = Math.Round((decimal)fbCovered / fbValid * 100m, 1);
                }

                // Feature architecture violations
                int featureArchViolations = architectureViolations.Count(v =>
                    ProjectDiscovery.FindFeatureForFile(v.FilePath)?.Equals(feature, StringComparison.OrdinalIgnoreCase) == true ||
                    v.SourceNamespace.Contains($".{feature}.", StringComparison.OrdinalIgnoreCase));

                // Feature complexity
                var featureMethods = complexityAnalysis.Methods
                    .Where(m => ProjectDiscovery.FindFeatureForFile(m.FilePath)?.Equals(feature, StringComparison.OrdinalIgnoreCase) == true)
                    .ToList();

                int featureMaxComplexity = featureMethods.Count > 0 ? featureMethods.Max(m => m.CyclomaticComplexity) : 1;

                featureMetricsDict[feature] = new FeatureMetrics(
                    LineCoverage: featureLineCoverage,
                    BranchCoverage: featureBranchCoverage,
                    ArchitectureViolations: featureArchViolations,
                    MaxComplexity: featureMaxComplexity,
                    TotalLines: featureTotalLines);
            }

            var toolVersion = ToolVersion.Current;
            var baseline = new QualityBaseline
            {
                SchemaVersion = 1,
                Commit = headCommit,
                Timestamp = DateTimeOffset.UtcNow,
                ToolVersion = toolVersion,
                Metrics = globalMetrics,
                Features = featureMetricsDict
            };

            // 9. Save Baseline File
            await BaselineLoader.SaveAsync(baseline, outputPath, workingDir, cancellationToken).ConfigureAwait(false);

            // Cleanup temp artifacts (only on success)
            try { Directory.Delete(tempArtifactDir, recursive: true); } catch { }

            // 10. Display Summary
            var resolvedOutputPath = BaselineLoader.ResolvePath(outputPath, workingDir);
            Console.WriteLine($"✅ Baseline successfully recorded to: {resolvedOutputPath}");
            Console.WriteLine();
            Console.WriteLine($"Global Metrics:");
            Console.WriteLine($"  - Commit: {headCommit[..Math.Min(8, headCommit.Length)]}");
            Console.WriteLine($"  - Line Coverage: {globalMetrics.LineCoverage:0.#}%");
            Console.WriteLine($"  - Total Tests: {globalMetrics.TotalTests} (Failed: {globalMetrics.FailedTests})");
            Console.WriteLine($"  - Compiler Warnings: {globalMetrics.TotalWarnings}");
            Console.WriteLine($"  - Architecture Violations: {globalMetrics.ArchitectureViolations}");
            Console.WriteLine($"  - Max Cyclomatic Complexity: {globalMetrics.MaxCyclomaticComplexity}");
            Console.WriteLine();
            Console.WriteLine($"Features Recorded ({featureMetricsDict.Count}):");
            foreach (var (feat, m) in featureMetricsDict)
            {
                var covStr = m.LineCoverage.HasValue ? $"{m.LineCoverage.Value:0.#}%" : "N/A";
                Console.WriteLine($"  - {feat,-16}: Coverage={covStr,-6} Violations={m.ArchitectureViolations,-3} MaxComplexity={m.MaxComplexity}");
            }
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error recording baseline: {ex.Message}");
            return 3;
        }
    }

    public static string FindRepositoryRoot(string startDir)
    {
        var current = new DirectoryInfo(startDir);
        while (current != null)
        {
            if (File.Exists(Path.Combine(current.FullName, "Wamage.sln")) ||
                File.Exists(Path.Combine(current.FullName, ".git", "HEAD")) ||
                Directory.Exists(Path.Combine(current.FullName, ".git")) ||
                Directory.Exists(Path.Combine(current.FullName, "backend")))
            {
                return current.FullName;
            }
            current = current.Parent;
        }
        return startDir;
    }
}
