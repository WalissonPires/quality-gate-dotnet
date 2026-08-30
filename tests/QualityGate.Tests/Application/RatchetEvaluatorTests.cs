using FluentAssertions;
using QualityGate.Application;
using QualityGate.Domain;
using QualityGate.Infrastructure.Coverage;
using Xunit;

namespace QualityGate.Tests.Application;

public sealed class RatchetEvaluatorTests
{
    private readonly QualityBaseline _baseline = new()
    {
        SchemaVersion = 1,
        Commit = "main-commit",
        Metrics = new GlobalMetrics(
            LineCoverage: 80.0m,
            BranchCoverage: 70.0m,
            TotalTests: 100,
            FailedTests: 0,
            TotalWarnings: 0,
            ArchitectureViolations: 5,
            MaxCyclomaticComplexity: 10),
        Features = new Dictionary<string, FeatureMetrics>
        {
            ["Persons"] = new(LineCoverage: 85.0m, BranchCoverage: 75.0m, ArchitectureViolations: 0, MaxComplexity: 8, TotalLines: 200),
            ["Channels"] = new(LineCoverage: 60.0m, BranchCoverage: 50.0m, ArchitectureViolations: 3, MaxComplexity: 12, TotalLines: 300)
        }
    };

    [Fact]
    public void Evaluate_WhenAllMetricsEqualOrBetter_ReturnsPassed()
    {
        var target = QualityTarget.ForDiff();
        var gates = new List<GateResult>
        {
            GateResult.Pass("Architecture", "5 violations", TimeSpan.Zero, actual: 4), // 4 <= 5
            GateResult.Pass("StaticAnalysis", "0 warnings", TimeSpan.Zero, actual: 0),
            GateResult.Pass("Coverage", "82% coverage", TimeSpan.Zero, actual: 82.0m)
        };

        var currentResult = new QualityResult(true, target, gates, TimeSpan.FromSeconds(2), "1.0.0", "run1");

        var result = RatchetEvaluator.Evaluate(currentResult, _baseline);

        result.Passed.Should().BeTrue();
        result.Findings.Should().BeEmpty();
    }

    [Fact]
    public void Evaluate_WhenArchitectureViolationsIncrease_ReturnsFailed()
    {
        var target = QualityTarget.ForDiff();
        var gates = new List<GateResult>
        {
            GateResult.Pass("Architecture", "6 violations", TimeSpan.Zero, actual: 6), // 6 > 5 (regressed)
            GateResult.Pass("StaticAnalysis", "0 warnings", TimeSpan.Zero, actual: 0)
        };

        var currentResult = new QualityResult(true, target, gates, TimeSpan.FromSeconds(2), "1.0.0", "run1");

        var result = RatchetEvaluator.Evaluate(currentResult, _baseline);

        result.Passed.Should().BeFalse();
        result.Findings.Should().ContainSingle();
        result.Findings[0].Rule.Should().Be("Ratchet.ArchitectureViolations");
    }

    [Fact]
    public void Evaluate_WhenFeatureCoverageDrops_ReturnsFailed()
    {
        var target = QualityTarget.ForDiff();
        var gates = new List<GateResult>
        {
            GateResult.Pass("Architecture", "5 violations", TimeSpan.Zero, actual: 5),
            GateResult.Pass("StaticAnalysis", "0 warnings", TimeSpan.Zero, actual: 0)
        };

        var currentResult = new QualityResult(true, target, gates, TimeSpan.FromSeconds(2), "1.0.0", "run1");

        // Persons feature coverage dropped from 85% to 70% (70 covered out of 100)
        var fileReport = new FileCoverageReport(
            "backend/Features/Persons/Person.cs",
            100, 70, 0, 0,
            new Dictionary<int, LineCoverageInfo>());

        var covSummary = new CoverageSummary(
            100, 70, 0, 0,
            new Dictionary<string, FileCoverageReport>
            {
                ["backend/Features/Persons/Person.cs"] = fileReport
            });

        var result = RatchetEvaluator.Evaluate(currentResult, _baseline, covSummary, ["Persons"]);

        result.Passed.Should().BeFalse();
        result.Findings.Should().Contain(f => f.Rule == "Ratchet.FeatureCoverage");
    }
}
