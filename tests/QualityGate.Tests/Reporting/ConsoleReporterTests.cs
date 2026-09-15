using System.Text;
using FluentAssertions;
using QualityGate.Domain;
using QualityGate.Reporting;
using Xunit;

namespace QualityGate.Tests.Reporting;

public sealed class ConsoleReporterTests
{
    [Fact]
    public async Task ReportAsync_OutputsExpectedSections()
    {
        var target = QualityTarget.ForDiff();
        var changeSet = new ChangeSet("base1234", "head5678", [new ChangedFile("src/App.cs", ChangeType.Modified)]);
        var gates = new List<GateResult>
        {
            GateResult.Pass("Build", "Build succeeded.", TimeSpan.FromSeconds(1)),
            GateResult.Fail("Coverage", "Coverage below threshold.", TimeSpan.FromSeconds(2),
                [new GateFinding("Coverage", "2 of 5 lines covered.", "Error", "src/App.cs", "Run", 10, 40, 80)])
        };

        var qualityResult = new QualityResult(false, target, gates, TimeSpan.FromSeconds(3), "1.0.0", "run-12345678", changeSet);

        var reporter = new ConsoleReporter(".artifacts/report.json");
        var sb = new StringBuilder();
        using var writer = new StringWriter(sb);

        await reporter.ReportAsync(qualityResult, writer);

        var output = sb.ToString();
        output.Should().Contain("Wamage Quality Gate v1.0.0");
        output.Should().Contain("Run: run-1234");
        output.Should().Contain("Scope");
        output.Should().Contain("Type: Diff");
        output.Should().Contain("Base: base123");
        output.Should().Contain("Head: head567");
        output.Should().Contain("BUILD");
        output.Should().Contain("PASS");
        output.Should().Contain("COVERAGE");
        output.Should().Contain("FAIL");
        output.Should().Contain("src/App.cs");
        output.Should().Contain("Run: 2 of 5 lines covered.");
        output.Should().Contain("QUALITY GATE: FAIL");
        output.Should().Contain("Full report: .artifacts/report.json");
    }

    [Fact]
    public async Task ReportAsync_WithMultipleReportArtifactPaths_OutputsAllPaths()
    {
        var target = QualityTarget.ForRepository();
        var qualityResult = new QualityResult(true, target, [], TimeSpan.Zero, "1.0.0", "run-12345678");

        var reporter = new ConsoleReporter([".artifacts/report.json", ".artifacts/report.md"]);
        var sb = new StringBuilder();
        using var writer = new StringWriter(sb);

        await reporter.ReportAsync(qualityResult, writer);

        var output = sb.ToString();
        output.Should().Contain("Full reports:");
        output.Should().Contain("- .artifacts/report.json");
        output.Should().Contain("- .artifacts/report.md");
    }

    [Fact]
    public async Task ReportAsync_WhenRatchetPresent_OutputsRatchetSectionAndOverallStatus()
    {
        var target = QualityTarget.ForDiff();
        var gates = new List<GateResult>
        {
            GateResult.Pass("Build", "Build succeeded.", TimeSpan.FromSeconds(1))
        };
        var ratchetResult = new QualityGate.Application.RatchetEvaluationResult(
            Passed: false,
            Findings: [new GateFinding("Ratchet.FeatureCoverage", "Coverage drop", "Error")],
            Summaries: ["❌ Line coverage for feature 'Channels' decreased by 0.7%."]);

        var qualityResult = new QualityResult(true, target, gates, TimeSpan.FromSeconds(2), "1.0.0", "run-12345678", null, ratchetResult);

        var reporter = new ConsoleReporter(".artifacts/report.json");
        var sb = new StringBuilder();
        using var writer = new StringWriter(sb);

        await reporter.ReportAsync(qualityResult, writer);

        var output = sb.ToString();
        output.Should().Contain("QUALITY RATCHET VERIFICATION:");
        output.Should().Contain("❌ Line coverage for feature 'Channels' decreased by 0.7%.");
        output.Should().Contain("❌ RATCHET VIOLATION: Quality metrics have regressed compared to baseline.");
        output.Should().Contain("QUALITY GATE: FAIL");
    }
}
