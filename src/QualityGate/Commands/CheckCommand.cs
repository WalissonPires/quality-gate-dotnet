using System.CommandLine;
using System.Reflection;
using QualityGate.Application;
using QualityGate.Configuration;
using QualityGate.Domain;
using QualityGate.Gates;
using QualityGate.Infrastructure.Coverage;
using QualityGate.Infrastructure.Dotnet;
using QualityGate.Infrastructure.Git;
using QualityGate.Infrastructure.Process;
using QualityGate.Reporting;

namespace QualityGate.Commands;

public static class CheckCommand
{
    public static Command Create(
        IProcessRunner? processRunner = null,
        IGitService? gitService = null,
        IDotnetService? dotnetService = null,
        ICoverageParser? coverageParser = null,
        IEnumerable<IQualityGate>? customGates = null)
    {
        var command = new Command("check", "Run code quality gates against the specified target");

        var diffOption = new Option<bool>("--diff", "Evaluate only changed code (default in git repository)");
        var namespaceOption = new Option<string?>("--namespace", "Evaluate a specific namespace");
        var projectOption = new Option<string?>("--project", "Evaluate a specific .csproj project");
        var repositoryOption = new Option<bool>("--repository", "Evaluate the entire repository");
        var baseOption = new Option<string?>("--base", "Git base reference for diff comparison");
        var formatOption = new Option<string>("--format", () => "console", "Output format: console, json, both");
        var skipOption = new Option<string[]>("--skip", "Gates to skip (comma-separated or multiple)") { AllowMultipleArgumentsPerToken = true };
        var onlyOption = new Option<string[]>("--only", "Only run these gates (comma-separated or multiple)") { AllowMultipleArgumentsPerToken = true };
        var failFastOption = new Option<bool>("--fail-fast", "Stop after first gate failure");
        var verboseOption = new Option<bool>("--verbose", "Enable detailed diagnostic output");
        var configOption = new Option<string>("--config", () => "qualitygate.json", "Path to configuration file");
        var ratchetOption = new Option<bool>("--ratchet", "Enable ratchet mode to prevent quality regression against baseline");
        var baselineOption = new Option<string?>("--baseline", "Path to baseline JSON file for ratchet comparison (default: .qualitygate/baseline.json)");

        command.AddOption(diffOption);
        command.AddOption(namespaceOption);
        command.AddOption(projectOption);
        command.AddOption(repositoryOption);
        command.AddOption(baseOption);
        command.AddOption(formatOption);
        command.AddOption(skipOption);
        command.AddOption(onlyOption);
        command.AddOption(failFastOption);
        command.AddOption(verboseOption);
        command.AddOption(configOption);
        command.AddOption(ratchetOption);
        command.AddOption(baselineOption);

        command.SetHandler(async (context) =>
        {
            var diff = context.ParseResult.GetValueForOption(diffOption);
            var ns = context.ParseResult.GetValueForOption(namespaceOption);
            var project = context.ParseResult.GetValueForOption(projectOption);
            var repository = context.ParseResult.GetValueForOption(repositoryOption);
            var baseRef = context.ParseResult.GetValueForOption(baseOption);
            var format = context.ParseResult.GetValueForOption(formatOption) ?? "console";
            var skip = context.ParseResult.GetValueForOption(skipOption) ?? [];
            var only = context.ParseResult.GetValueForOption(onlyOption) ?? [];
            var failFast = context.ParseResult.GetValueForOption(failFastOption);
            var verbose = context.ParseResult.GetValueForOption(verboseOption);
            var configPath = context.ParseResult.GetValueForOption(configOption) ?? "qualitygate.json";
            var ratchet = context.ParseResult.GetValueForOption(ratchetOption);
            var baselinePath = context.ParseResult.GetValueForOption(baselineOption);

            var exitCode = await ExecuteAsync(
                diff, ns, project, repository, baseRef, format, skip, only, failFast, verbose, configPath,
                ratchet, baselinePath,
                processRunner, gitService, dotnetService, coverageParser, customGates, context.GetCancellationToken());

            context.ExitCode = exitCode;
        });

        return command;
    }

    public static async Task<int> ExecuteAsync(
        bool diff,
        string? ns,
        string? project,
        bool repository,
        string? baseRef,
        string format,
        string[] skip,
        string[] only,
        bool failFast,
        bool verbose,
        string configPath,
        bool ratchet = false,
        string? baselinePath = null,
        IProcessRunner? processRunner = null,
        IGitService? gitService = null,
        IDotnetService? dotnetService = null,
        ICoverageParser? coverageParser = null,
        IEnumerable<IQualityGate>? customGates = null,
        CancellationToken cancellationToken = default)
    {
        // Mutual exclusivity of skip and only
        var flatSkip = FlattenOptions(skip);
        var flatOnly = FlattenOptions(only);
        if (flatSkip.Count > 0 && flatOnly.Count > 0)
        {
            Console.Error.WriteLine("Error: --skip and --only are mutually exclusive.");
            return 2; // ConfigurationError
        }

        format = format.ToLowerInvariant();
        if (format is not ("console" or "json" or "both"))
        {
            Console.Error.WriteLine($"Error: Invalid format '{format}'. Supported formats: console, json, both.");
            return 2;
        }

        try
        {
            var workingDir = Environment.CurrentDirectory;

            // Load configuration
            QualityGateOptions options;
            if (File.Exists(configPath))
            {
                options = await ConfigurationLoader.LoadAsync(configPath, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                var dotQualityGateConfig = Path.Combine(workingDir, ".qualitygate", "qualitygate.json");
                options = File.Exists(dotQualityGateConfig)
                    ? await ConfigurationLoader.LoadAsync(dotQualityGateConfig, cancellationToken).ConfigureAwait(false)
                    : new QualityGateOptions();
            }

            if (flatOnly.Contains("mutation", StringComparer.OrdinalIgnoreCase))
            {
                options.Mutation.Enabled = true;
            }

            // Infrastructure services
            processRunner ??= new ProcessRunner();
            gitService ??= new GitService(processRunner);
            dotnetService ??= new DotnetService(processRunner);
            coverageParser ??= new CoberturaParser();

            // Tool availability pre-flight check
            var dotnetCheck = await processRunner.RunAsync(new ProcessRequest("dotnet", ["--version"], workingDir, TimeSpan.FromSeconds(10)), cancellationToken).ConfigureAwait(false);
            if (dotnetCheck.ExitCode != 0)
            {
                Console.Error.WriteLine("Error: .NET SDK ('dotnet') is not available on PATH or failed to execute.");
                return 3; // InfrastructureError
            }

            if (verbose)
            {
                Console.WriteLine($"[Verbose] .NET SDK Version: {dotnetCheck.StandardOutput.Trim()}");
                Console.WriteLine($"[Verbose] Working Directory: {workingDir}");
                Console.WriteLine($"[Verbose] Target Scope: diff={diff}, ns={ns}, proj={project}, repo={repository}, ratchet={ratchet}");
            }

            // Resolve target
            var targetResolver = new TargetResolver(gitService, workingDir);
            var target = await targetResolver.ResolveAsync(diff, ns, project, repository, cancellationToken).ConfigureAwait(false);

            // Resolve ChangeSet if Diff scope
            ChangeSet? changeSet = null;
            if (target.Scope == QualityScope.Diff)
            {
                changeSet = await gitService.GetChangeSetAsync(workingDir, baseRef, cancellationToken).ConfigureAwait(false);
            }

            // Project discovery
            var discovery = new ProjectDiscovery(workingDir);
            var allProjects = discovery.FindAllProjects();

            IReadOnlyList<string> affectedProjects;
            IReadOnlyList<string> testProjects;

            if (target.Scope == QualityScope.Project && !string.IsNullOrWhiteSpace(target.Project))
            {
                affectedProjects = [target.Project];
                testProjects = discovery.FindTestProjectsFor([target.Project], allProjects);
            }
            else if (target.Scope == QualityScope.Namespace && !string.IsNullOrWhiteSpace(target.Namespace))
            {
                var nsFiles = discovery.FindSourceFilesForNamespace(target.Namespace);
                var owningProjects = nsFiles.Select(f => discovery.FindOwningProject(f, allProjects))
                    .Where(p => !string.IsNullOrWhiteSpace(p))
                    .Distinct()
                    .Cast<string>()
                    .ToList();
                affectedProjects = owningProjects.Count > 0 ? owningProjects : allProjects.Where(p => !discovery.IsTestProject(p)).ToList();
                testProjects = discovery.FindTestProjectsFor(affectedProjects, allProjects);
            }
            else if (target.Scope == QualityScope.Repository)
            {
                affectedProjects = allProjects.Where(p => !discovery.IsTestProject(p)).ToList();
                testProjects = allProjects.Where(discovery.IsTestProject).ToList();
            }
            else if (changeSet != null)
            {
                affectedProjects = discovery.FindAffectedProjects(changeSet, allProjects);
                testProjects = discovery.FindTestProjectsFor(affectedProjects, allProjects);
            }
            else
            {
                affectedProjects = allProjects.Where(p => !discovery.IsTestProject(p)).ToList();
                testProjects = allProjects.Where(discovery.IsTestProject).ToList();
            }

            // Per-run setup
            var runId = Guid.NewGuid().ToString("N");
            var artifactDir = Path.Combine(workingDir, ".qualitygate", "artifacts", runId);
            Directory.CreateDirectory(artifactDir);
            var reportJsonPath = Path.Combine(artifactDir, "report.json");

            var context = new QualityContext(
                target,
                affectedProjects,
                testProjects,
                options,
                artifactDir,
                runId,
                cancellationToken,
                changeSet);

            // Instantiate default gates if none provided
            var architectureValidator = new QualityGate.Infrastructure.Architecture.ArchitectureValidator();
            var complexityAnalyzer = new QualityGate.Infrastructure.Roslyn.ComplexityAnalyzer();
            var strykerService = new QualityGate.Infrastructure.Mutation.StrykerService(processRunner);

            var gates = customGates?.ToList() ?? new List<IQualityGate>
            {
                new BuildGate(dotnetService),
                new TestGate(dotnetService),
                new CoverageGate(coverageParser),
                new ComplexityGate(complexityAnalyzer),
                new StaticAnalysisGate(dotnetService),
                new ArchitectureGate(architectureValidator),
                new MutationGate(strykerService)
            };

            var toolVersion = ToolVersion.Current;
            var runner = new QualityGateRunner(gates, toolVersion);

            var qualityResult = await runner.RunAsync(
                context,
                skipGates: flatSkip,
                onlyGates: flatOnly,
                failFastOverride: failFast,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            // Ratchet Evaluation if requested
            bool ratchetPassed = true;
            RatchetEvaluationResult? ratchetResult = null;

            if (ratchet)
            {
                var baseline = await BaselineLoader.LoadAsync(baselinePath, workingDir, cancellationToken).ConfigureAwait(false);
                if (baseline == null)
                {
                    var resolvedBaselinePath = BaselineLoader.ResolvePath(baselinePath, workingDir);
                    Console.Error.WriteLine($"⚠️  Warning: Ratchet mode enabled, but baseline file was not found at: '{resolvedBaselinePath}'. Ratchet checks skipped.");
                }
                else
                {
                    // Discover coverage summary for ratchet feature evaluation
                    var coverageFiles = Directory.Exists(artifactDir)
                        ? Directory.GetFiles(artifactDir, "coverage.cobertura.xml", SearchOption.AllDirectories)
                        : [];
                    var covSummary = await coverageParser.ParseMultipleAsync(coverageFiles, cancellationToken).ConfigureAwait(false);

                    var affectedFeatures = changeSet != null
                        ? changeSet.NonDeletedFiles
                            .Select(f => ProjectDiscovery.FindFeatureForFile(f.Path))
                            .Where(f => !string.IsNullOrEmpty(f))
                            .Distinct()
                            .Cast<string>()
                            .ToList()
                        : discovery.DiscoverFeatures();

                    ratchetResult = RatchetEvaluator.Evaluate(qualityResult, baseline, covSummary, affectedFeatures);
                    ratchetPassed = ratchetResult.Passed;
                }
            }

            // Generate reports
            var jsonReporter = new JsonReporter();
            var jsonContent = jsonReporter.Serialize(qualityResult);
            await File.WriteAllTextAsync(reportJsonPath, jsonContent, cancellationToken).ConfigureAwait(false);

            if (format == "json")
            {
                Console.WriteLine(jsonContent);
            }
            else if (format is "console" or "both")
            {
                var consoleReporter = new ConsoleReporter(reportJsonPath);
                await consoleReporter.ReportAsync(qualityResult, Console.Out, cancellationToken).ConfigureAwait(false);

                if (ratchetResult != null)
                {
                    Console.WriteLine();
                    Console.WriteLine("QUALITY RATCHET VERIFICATION:");
                    foreach (var summary in ratchetResult.Summaries)
                    {
                        Console.WriteLine($"  {summary}");
                    }

                    if (!ratchetResult.Passed)
                    {
                        Console.WriteLine("  ❌ RATCHET VIOLATION: Quality metrics have regressed compared to baseline.");
                    }
                }
            }

            // Cleanup artifacts if passed, or keep for diagnosis
            if (qualityResult.Passed && ratchetPassed)
            {
                try
                {
                    var testResultsDir = Path.Combine(artifactDir, "TestResults");
                    if (Directory.Exists(testResultsDir))
                    {
                        Directory.Delete(testResultsDir, recursive: true);
                    }
                }
                catch
                {
                    // Ignore cleanup failures
                }
            }

            // Exit code mapping
            if (qualityResult.Gates.Any(g => g.Status == GateStatus.Error))
            {
                return 3; // InfrastructureError
            }

            if (!qualityResult.Passed || !ratchetPassed)
            {
                return 1; // QualityFailure
            }

            return 0; // Success
        }
        catch (ConfigurationException ex)
        {
            Console.Error.WriteLine($"Configuration error: {ex.Message}");
            return 2;
        }
        catch (ScopeException ex)
        {
            Console.Error.WriteLine($"Scope error: {ex.Message}");
            return 4;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Console.Error.WriteLine($"Unexpected internal error: {ex}");
            return 5;
        }
    }

    private static List<string> FlattenOptions(string[] options)
    {
        return options
            .SelectMany(s => s.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .ToList();
    }
}
