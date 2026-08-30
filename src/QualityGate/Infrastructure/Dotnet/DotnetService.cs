using System.Text.RegularExpressions;
using QualityGate.Infrastructure.Process;

namespace QualityGate.Infrastructure.Dotnet;

public sealed partial class DotnetService : IDotnetService
{
    private readonly IProcessRunner _processRunner;
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromMinutes(5);

    public DotnetService(IProcessRunner processRunner)
    {
        _processRunner = processRunner ?? throw new ArgumentNullException(nameof(processRunner));
    }

    public async Task<DotnetBuildResult> BuildAsync(
        string projectOrSolutionPath,
        string configuration = "Release",
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        var fullProjectPath = Path.GetFullPath(projectOrSolutionPath);
        var workingDir = Path.GetDirectoryName(fullProjectPath) ?? Environment.CurrentDirectory;

        var args = new List<string> { "build", fullProjectPath, "-c", configuration };

        var request = new ProcessRequest(
            "dotnet",
            args,
            workingDir,
            timeout ?? DefaultTimeout);

        var result = await _processRunner.RunAsync(request, cancellationToken).ConfigureAwait(false);
        return new DotnetBuildResult(result.ExitCode, result.StandardOutput, result.StandardError, result.Duration, result.TimedOut);
    }

    public async Task<DotnetTestResult> TestAsync(
        string testProjectPath,
        string? resultsDirectory = null,
        bool collectCoverage = true,
        string configuration = "Release",
        bool noBuild = true,
        string? testFilter = null,
        string coverageFormat = "cobertura",
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        var fullTestProjectPath = Path.GetFullPath(testProjectPath);
        var workingDir = Path.GetDirectoryName(fullTestProjectPath) ?? Environment.CurrentDirectory;

        var args = new List<string> { "test", fullTestProjectPath, "-c", configuration };
        if (noBuild)
        {
            args.Add("--no-build");
        }

        if (!string.IsNullOrWhiteSpace(resultsDirectory))
        {
            args.Add("--results-directory");
            args.Add(resultsDirectory);
        }

        if (collectCoverage)
        {
            args.Add("--collect");
            args.Add($"XPlat Code Coverage;Format={coverageFormat}");
        }

        if (!string.IsNullOrWhiteSpace(testFilter))
        {
            args.Add("--filter");
            args.Add(testFilter);
        }

        var request = new ProcessRequest(
            "dotnet",
            args,
            workingDir,
            timeout ?? DefaultTimeout);

        var result = await _processRunner.RunAsync(request, cancellationToken).ConfigureAwait(false);

        var (total, passed, failed, skipped) = ParseTestSummary(result.StandardOutput);

        string? coverageFile = null;
        if (collectCoverage && !string.IsNullOrWhiteSpace(resultsDirectory) && Directory.Exists(resultsDirectory))
        {
            coverageFile = Directory.GetFiles(resultsDirectory, "coverage.cobertura.xml", SearchOption.AllDirectories)
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .FirstOrDefault();
        }

        return new DotnetTestResult(
            result.ExitCode,
            total,
            passed,
            failed,
            skipped,
            result.StandardOutput,
            result.StandardError,
            result.Duration,
            result.TimedOut,
            coverageFile);
    }

    public static (int Total, int Passed, int Failed, int Skipped) ParseTestSummary(string output)
    {
        if (string.IsNullOrWhiteSpace(output))
        {
            return (0, 0, 0, 0);
        }

        // Pattern: Total tests: X. Passed: Y. Failed: Z. Skipped: W.
        // or Portuguese: Total de testes: X. Aprovado: Y. Com falha: Z. Ignorado: W.
        // or xUnit: Aprovado! – Com falha: 0, Aprovado: 38, Ignorado: 0, Total: 38

        int total = 0, passed = 0, failed = 0, skipped = 0;

        var matchTotal = Regex.Match(output, @"(?:Total(?: tests| de testes)?:?|Total:)\s*(\d+)", RegexOptions.IgnoreCase);
        if (matchTotal.Success && int.TryParse(matchTotal.Groups[1].Value, out var t)) total = t;

        var matchPassed = Regex.Match(output, @"(?:Passed|Aprovado)s?:?\s*(\d+)", RegexOptions.IgnoreCase);
        if (matchPassed.Success && int.TryParse(matchPassed.Groups[1].Value, out var p)) passed = p;

        var matchFailed = Regex.Match(output, @"(?:Failed|Com falha)s?:?\s*(\d+)", RegexOptions.IgnoreCase);
        if (matchFailed.Success && int.TryParse(matchFailed.Groups[1].Value, out var f)) failed = f;

        var matchSkipped = Regex.Match(output, @"(?:Skipped|Ignorado)s?:?\s*(\d+)", RegexOptions.IgnoreCase);
        if (matchSkipped.Success && int.TryParse(matchSkipped.Groups[1].Value, out var s)) skipped = s;

        if (total == 0)
        {
            total = passed + failed + skipped;
        }

        return (total, passed, failed, skipped);
    }
}
