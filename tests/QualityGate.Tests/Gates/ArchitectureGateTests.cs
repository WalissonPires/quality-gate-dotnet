using FluentAssertions;
using NSubstitute;
using QualityGate.Configuration;
using QualityGate.Domain;
using QualityGate.Gates;
using QualityGate.Infrastructure.Architecture;
using Xunit;

namespace QualityGate.Tests.Gates;

public sealed class ArchitectureGateTests
{
    private readonly IArchitectureValidator _validator = Substitute.For<IArchitectureValidator>();
    private readonly ArchitectureGate _gate;

    public ArchitectureGateTests()
    {
        _gate = new ArchitectureGate(_validator);
    }

    [Fact]
    public async Task ExecuteAsync_WhenNoViolations_ReturnsPass()
    {
        _validator.ValidateAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<IEnumerable<ArchitectureRuleOptions>>(), Arg.Any<CancellationToken>())
            .Returns([]);

        var options = new QualityGateOptions();
        options.Architecture.Rules = [new() { Name = "TestRule", Source = "A", ForbiddenDependencies = ["B"] }];

        var context = new QualityContext(
            QualityTarget.ForDiff(),
            affectedProjects: ["backend/Wamage.csproj"],
            testProjects: [],
            options: options,
            artifactDirectory: ".artifacts/test",
            runId: "run1",
            changeSet: new ChangeSet("base", "head", [new ChangedFile("src/File.cs", ChangeType.Modified)]));

        var result = await _gate.ExecuteAsync(context);

        result.Passed.Should().BeTrue();
        result.Status.Should().Be(GateStatus.Passed);
        result.Actual.Should().Be(0);
    }

    [Fact]
    public async Task ExecuteAsync_WhenViolationsFound_ReturnsFailWithFindings()
    {
        var violation = new ArchitectureViolation("DomainIsolation", "backend/Person.cs", 5, "Domain", "EFCore", "Violation");
        _validator.ValidateAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<IEnumerable<ArchitectureRuleOptions>>(), Arg.Any<CancellationToken>())
            .Returns([violation]);

        var options = new QualityGateOptions();
        options.Architecture.Rules = [new() { Name = "DomainIsolation", Source = "Domain", ForbiddenDependencies = ["EFCore"] }];

        var context = new QualityContext(
            QualityTarget.ForDiff(),
            affectedProjects: ["backend/Wamage.csproj"],
            testProjects: [],
            options: options,
            artifactDirectory: ".artifacts/test",
            runId: "run1",
            changeSet: new ChangeSet("base", "head", [new ChangedFile("backend/Person.cs", ChangeType.Modified)]));

        var result = await _gate.ExecuteAsync(context);

        result.Passed.Should().BeFalse();
        result.Status.Should().Be(GateStatus.Failed);
        result.Actual.Should().Be(1);
        result.Findings.Should().ContainSingle();
    }

    [Fact]
    public async Task ExecuteAsync_WhenDiffScopeAndViolationsWithinFeatureBaseline_ReturnsPass()
    {
        var violation = new ArchitectureViolation("ApplicationIsolation", "backend/Features/Notifications/NotificationDispatcher.cs", 7, "wamage.Features.Notifications", "wamage.Infrastructure", "Violation");
        _validator.ValidateAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<IEnumerable<ArchitectureRuleOptions>>(), Arg.Any<CancellationToken>())
            .Returns([violation]);

        var options = new QualityGateOptions();
        options.Architecture.Rules = [new() { Name = "ApplicationIsolation", Source = "*.Application", ForbiddenDependencies = ["*.Infrastructure"] }];

        var baseline = new QualityBaseline
        {
            Features = new Dictionary<string, FeatureMetrics>
            {
                ["Notifications"] = new(LineCoverage: 80m, BranchCoverage: 70m, ArchitectureViolations: 17, MaxComplexity: 22)
            }
        };

        var context = new QualityContext(
            QualityTarget.ForDiff(),
            affectedProjects: ["backend/Wamage.csproj"],
            testProjects: [],
            options: options,
            artifactDirectory: ".artifacts/test",
            runId: "run1",
            changeSet: new ChangeSet("base", "head", [new ChangedFile("backend/Features/Notifications/NotificationDispatcher.cs", ChangeType.Modified)]),
            baseline: baseline,
            ratchet: true);

        var result = await _gate.ExecuteAsync(context);

        result.Passed.Should().BeTrue();
        result.Status.Should().Be(GateStatus.Passed);
        result.Actual.Should().Be(1);
        result.Findings.Should().ContainSingle();
        result.Findings[0].Severity.Should().Be("Warning");
    }

    [Fact]
    public async Task ExecuteAsync_WhenDiffScopeAndViolationsExceedFeatureBaseline_ReturnsFail()
    {
        var violation1 = new ArchitectureViolation("ApplicationIsolation", "backend/Features/Notifications/NotificationDispatcher.cs", 7, "wamage.Features.Notifications", "wamage.Infrastructure", "Violation 1");
        var violation2 = new ArchitectureViolation("ApplicationIsolation", "backend/Features/Notifications/NotificationDispatcher.cs", 8, "wamage.Features.Notifications", "wamage.Infrastructure", "Violation 2");
        _validator.ValidateAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<IEnumerable<ArchitectureRuleOptions>>(), Arg.Any<CancellationToken>())
            .Returns([violation1, violation2]);

        var options = new QualityGateOptions();
        options.Architecture.Rules = [new() { Name = "ApplicationIsolation", Source = "*.Application", ForbiddenDependencies = ["*.Infrastructure"] }];

        var baseline = new QualityBaseline
        {
            Features = new Dictionary<string, FeatureMetrics>
            {
                ["Notifications"] = new(LineCoverage: 80m, BranchCoverage: 70m, ArchitectureViolations: 1, MaxComplexity: 22)
            }
        };

        var context = new QualityContext(
            QualityTarget.ForDiff(),
            affectedProjects: ["backend/Wamage.csproj"],
            testProjects: [],
            options: options,
            artifactDirectory: ".artifacts/test",
            runId: "run1",
            changeSet: new ChangeSet("base", "head", [new ChangedFile("backend/Features/Notifications/NotificationDispatcher.cs", ChangeType.Modified)]),
            baseline: baseline,
            ratchet: true);

        var result = await _gate.ExecuteAsync(context);

        result.Passed.Should().BeFalse();
        result.Status.Should().Be(GateStatus.Failed);
        result.Actual.Should().Be(2);
        result.Findings.Should().Contain(f => f.Severity == "Error");
    }

    [Fact]
    public async Task ExecuteAsync_WhenRepositoryScopeAndViolationsWithinGlobalBaseline_ReturnsPass()
    {
        var violations = Enumerable.Range(1, 24)
            .Select(i => new ArchitectureViolation("ApplicationIsolation", $"backend/File{i}.cs", i, "Domain", "Infra", $"Violation {i}"))
            .ToList();
        _validator.ValidateAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<IEnumerable<ArchitectureRuleOptions>>(), Arg.Any<CancellationToken>())
            .Returns(violations);

        var options = new QualityGateOptions();
        options.Architecture.Rules = [new() { Name = "ApplicationIsolation", Source = "*.Application", ForbiddenDependencies = ["*.Infrastructure"] }];

        var baseline = new QualityBaseline
        {
            Metrics = new GlobalMetrics(null, null, 0, 0, 0, ArchitectureViolations: 24, MaxCyclomaticComplexity: 10)
        };

        var context = new QualityContext(
            QualityTarget.ForRepository(),
            affectedProjects: ["backend/Wamage.csproj"],
            testProjects: [],
            options: options,
            artifactDirectory: ".artifacts/test",
            runId: "run1",
            baseline: baseline,
            ratchet: true);

        var result = await _gate.ExecuteAsync(context);

        result.Passed.Should().BeTrue();
        result.Status.Should().Be(GateStatus.Passed);
        result.Actual.Should().Be(24);
    }
}
