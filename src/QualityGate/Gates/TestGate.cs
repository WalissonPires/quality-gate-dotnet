using System.Text.RegularExpressions;
using QualityGate.Domain;
using QualityGate.Infrastructure.Dotnet;

namespace QualityGate.Gates;

public sealed partial class TestGate : IQualityGate
{
    private readonly IDotnetService _dotnetService;

    public string Name => "Test";

    public TestGate(IDotnetService dotnetService)
    {
        _dotnetService = dotnetService ?? throw new ArgumentNullException(nameof(dotnetService));
    }

    public async Task<GateResult> ExecuteAsync(QualityContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (!context.Options.Execution.UnitTests)
        {
            return GateResult.Skip(Name, "Unit tests execution is disabled in configuration.");
        }

        var testProjects = context.TestProjects.ToList();
        if (testProjects.Count == 0)
        {
            if (context.AffectedProjects.Count > 0)
            {
                return GateResult.Fail(
                    Name,
                    "No test projects found for the affected code.",
                    TimeSpan.Zero,
                    [new GateFinding("MissingTests", "No test projects associated with affected source code were found.", "Error")]);
            }

            return GateResult.NotApplicable(Name, "No test projects to execute.");
        }

        int totalPassed = 0;
        int totalFailed = 0;
        int totalSkipped = 0;
        int totalTests = 0;
        var findings = new List<GateFinding>();
        TimeSpan totalDuration = TimeSpan.Zero;

        foreach (var testProject in testProjects)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var timeout = TimeSpan.FromSeconds(context.Options.Execution.ProcessTimeoutSeconds);
            var resultsDir = Path.Combine(context.ArtifactDirectory, "TestResults", Path.GetFileNameWithoutExtension(testProject));
            Directory.CreateDirectory(resultsDir);

            if (context.Verbose)
            {
                Console.WriteLine($"[Verbose] [Test] Executing test project: {testProject} (collecting coverage)...");
            }
            var testResult = await _dotnetService.TestAsync(
                testProject,
                resultsDir,
                collectCoverage: true,
                configuration: "Release",
                noBuild: true,
                timeout: timeout,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            totalDuration += testResult.Duration;

            if (context.Verbose)
                Console.WriteLine($"[Verbose] [Test] Completed {Path.GetFileName(testProject)}: {testResult.PassedTests} passed, {testResult.FailedTests} failed, {testResult.SkippedTests} skipped in {testResult.Duration.TotalSeconds:0.##}s");
            if (testResult.TimedOut)
            {
                return GateResult.Error(Name, $"Tests timed out for '{testProject}' after {timeout.TotalSeconds} seconds.", totalDuration);
            }

            totalPassed += testResult.PassedTests;
            totalFailed += testResult.FailedTests;
            totalSkipped += testResult.SkippedTests;
            totalTests += testResult.TotalTests;

            if (testResult.FailedTests > 0 || testResult.ExitCode != 0)
            {
                var failureFindings = ParseTestFailures(testResult.StandardOutput, testProject);
                findings.AddRange(failureFindings);
            }
        }

        if (totalFailed > 0)
        {
            return GateResult.Fail(
                Name,
                $"{totalFailed}/{totalTests} tests failed.",
                totalDuration,
                findings,
                actual: totalPassed);
        }

        return GateResult.Pass(
            Name,
            $"{totalPassed}/{totalTests} tests passed.",
            totalDuration,
            findings,
            actual: totalPassed);
    }

    public static IReadOnlyList<GateFinding> ParseTestFailures(string output, string testProject)
    {
        var findings = new List<GateFinding>();
        if (string.IsNullOrWhiteSpace(output))
        {
            findings.Add(new GateFinding("TestFailure", "Test run failed with non-zero exit code.", "Error", testProject));
            return findings;
        }

        // Pattern: [xUnit.net 00:00:01.23] Namespace.Class.Method [FAIL]
        // or Com falha/Failed Namespace.Class.Method
        var regex = new Regex(@"(?:\[FAIL\]\s*(?<test>\S+)|(?:Failed|Com falha)\s+(?<test>\S+))", RegexOptions.Multiline);
        var matches = regex.Matches(output);

        foreach (Match match in matches)
        {
            var testName = match.Groups["test"].Value.Trim();
            if (!string.IsNullOrEmpty(testName))
            {
                findings.Add(new GateFinding("TestFailure", $"Test '{testName}' failed.", "Error", testProject, testName));
            }
        }

        if (findings.Count == 0)
        {
            findings.Add(new GateFinding("TestFailure", "One or more tests failed.", "Error", testProject));
        }

        return findings;
    }
}
