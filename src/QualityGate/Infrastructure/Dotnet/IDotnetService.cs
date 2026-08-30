using QualityGate.Infrastructure.Process;

namespace QualityGate.Infrastructure.Dotnet;

public sealed record DotnetBuildResult(int ExitCode, string StandardOutput, string StandardError, TimeSpan Duration, bool TimedOut)
{
    public bool Succeeded => ExitCode == 0 && !TimedOut;
}

public sealed record DotnetTestResult(
    int ExitCode,
    int TotalTests,
    int PassedTests,
    int FailedTests,
    int SkippedTests,
    string StandardOutput,
    string StandardError,
    TimeSpan Duration,
    bool TimedOut,
    string? CoverageReportPath = null)
{
    public bool Succeeded => ExitCode == 0 && FailedTests == 0 && !TimedOut;
}

public interface IDotnetService
{
    Task<DotnetBuildResult> BuildAsync(
        string projectOrSolutionPath,
        string configuration = "Release",
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default);

    Task<DotnetTestResult> TestAsync(
        string testProjectPath,
        string? resultsDirectory = null,
        bool collectCoverage = true,
        string configuration = "Release",
        bool noBuild = true,
        string? testFilter = null,
        string coverageFormat = "cobertura",
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default);
}
