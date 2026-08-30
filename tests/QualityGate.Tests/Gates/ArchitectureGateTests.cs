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
}
