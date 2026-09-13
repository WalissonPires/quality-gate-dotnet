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
    public void Evaluate_WhenRepositoryScopeAndAllMetricsEqualOrBetter_ReturnsPassed()
    {
        var target = QualityTarget.ForRepository();
        var gates = new List<GateResult>
        {
            GateResult.Pass("Architecture", "4 violations", TimeSpan.Zero, actual: 4), // 4 <= 5
            GateResult.Pass("StaticAnalysis", "0 warnings", TimeSpan.Zero, actual: 0),
            GateResult.Pass("Coverage", "82% coverage", TimeSpan.Zero, actual: 82.0m)
        };

        var currentResult = new QualityResult(true, target, gates, TimeSpan.FromSeconds(2), "1.0.0", "run1");

        var result = RatchetEvaluator.Evaluate(currentResult, _baseline);

        result.Passed.Should().BeTrue();
        result.Findings.Should().BeEmpty();
        result.Summaries.Should().Contain(s => s.Contains("Global architecture violations: 4 <= 5"));
    }

    [Fact]
    public void Evaluate_WhenRepositoryScopeAndArchitectureViolationsIncrease_ReturnsFailed()
    {
        var target = QualityTarget.ForRepository();
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
    public void Evaluate_WhenDiffScope_DoesNotEvaluateGlobalArchitectureRatchet()
    {
        var target = QualityTarget.ForDiff();
        var gates = new List<GateResult>
        {
            GateResult.Pass("Architecture", "1 violation", TimeSpan.Zero, actual: 1), // 1 violation in diff
            GateResult.Pass("StaticAnalysis", "0 warnings", TimeSpan.Zero, actual: 0)
        };

        var currentResult = new QualityResult(true, target, gates, TimeSpan.FromSeconds(2), "1.0.0", "run1");

        var result = RatchetEvaluator.Evaluate(currentResult, _baseline);

        result.Passed.Should().BeTrue();
        result.Findings.Should().BeEmpty();
        // Should NOT contain misleading global architecture comparison
        result.Summaries.Should().NotContain(s => s.Contains("Global architecture violations"));
        result.Summaries.Should().Contain(s => s.Contains("Per-feature ratchet"));
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

    [Fact]
    public void Evaluate_WhenFeatureArchitectureViolationsExceedBaseline_ReturnsFailed()
    {
        var target = QualityTarget.ForDiff();
        var archFindings = new List<GateFinding>
        {
            new("ApplicationIsolation", "Violation 1", "Error", "backend/Features/Persons/Person.cs"),
            new("ApplicationIsolation", "Violation 2", "Error", "backend/Features/Persons/Person.cs")
        };
        var gates = new List<GateResult>
        {
            GateResult.Fail("Architecture", "2 violations", TimeSpan.Zero, archFindings, actual: 2)
        };

        var currentResult = new QualityResult(false, target, gates, TimeSpan.FromSeconds(2), "1.0.0", "run1");

        // Baseline has Persons ArchitectureViolations: 0
        var result = RatchetEvaluator.Evaluate(currentResult, _baseline, null, ["Persons"]);

        result.Passed.Should().BeFalse();
        result.Findings.Should().Contain(f => f.Rule == "Ratchet.FeatureArchitecture");
        result.Summaries.Should().Contain(s => s.Contains("❌ Architecture violations for feature 'Persons' increased by 2"));
    }

    [Fact]
    public void Evaluate_WhenFeatureComplexityExceedsBaseline_ReturnsFailed()
    {
        var target = QualityTarget.ForDiff();
        var compFindings = new List<GateFinding>
        {
            new("CyclomaticComplexity", "Complexity 15", "Error", "backend/Features/Persons/Person.cs", actual: 15)
        };
        var gates = new List<GateResult>
        {
            GateResult.Fail("Complexity", "Max complexity 15", TimeSpan.Zero, compFindings, actual: 15)
        };

        var currentResult = new QualityResult(false, target, gates, TimeSpan.FromSeconds(2), "1.0.0", "run1");

        // Baseline has Persons MaxComplexity: 8
        var result = RatchetEvaluator.Evaluate(currentResult, _baseline, null, ["Persons"]);

        result.Passed.Should().BeFalse();
        result.Findings.Should().Contain(f => f.Rule == "Ratchet.FeatureComplexity");
        result.Summaries.Should().Contain(s => s.Contains("❌ Max cyclomatic complexity for feature 'Persons' increased by 7"));
    }

    [Fact]
    public void Evaluate_WhenFeatureArchitectureAndComplexityWithinBaseline_ReturnsPassed()
    {
        var target = QualityTarget.ForDiff();
        var archFindings = new List<GateFinding>
        {
            new("ApplicationIsolation", "Violation 1", "Warning", "backend/Features/Channels/ChannelDispatcher.cs")
        };
        var compFindings = new List<GateFinding>
        {
            new("CyclomaticComplexity", "Complexity 10", "Warning", "backend/Features/Channels/ChannelDispatcher.cs", actual: 10)
        };
        var gates = new List<GateResult>
        {
            GateResult.Pass("Architecture", "1 violation within baseline", TimeSpan.Zero, archFindings, actual: 1),
            GateResult.Pass("Complexity", "Complexity 10 within baseline", TimeSpan.Zero, compFindings, actual: 10)
        };

        var currentResult = new QualityResult(true, target, gates, TimeSpan.FromSeconds(2), "1.0.0", "run1");

        // Channels in baseline: ArchitectureViolations: 3, MaxComplexity: 12
        var result = RatchetEvaluator.Evaluate(currentResult, _baseline, null, ["Channels"]);

        result.Passed.Should().BeTrue();
        result.Findings.Should().BeEmpty();
        result.Summaries.Should().Contain(s => s.Contains("Feature 'Channels' architecture violations: 1 <= 3"));
        result.Summaries.Should().Contain(s => s.Contains("Feature 'Channels' max complexity: 10 <= 12"));
    }
}
