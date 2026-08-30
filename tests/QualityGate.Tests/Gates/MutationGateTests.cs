using FluentAssertions;
using NSubstitute;
using QualityGate.Configuration;
using QualityGate.Domain;
using QualityGate.Gates;
using QualityGate.Infrastructure.Mutation;
using Xunit;

namespace QualityGate.Tests.Gates;

public sealed class MutationGateTests
{
    private readonly IStrykerService _strykerService = Substitute.For<IStrykerService>();
    private readonly MutationGate _gate;

    public MutationGateTests()
    {
        _gate = new MutationGate(_strykerService);
    }

    [Fact]
    public async Task ExecuteAsync_WhenDisabledInOptions_ReturnsSkipped()
    {
        var options = new QualityGateOptions();
        options.Mutation.Enabled = false;

        var context = new QualityContext(
            QualityTarget.ForDiff(),
            affectedProjects: ["backend/Wamage.csproj"],
            testProjects: [],
            options: options,
            artifactDirectory: ".artifacts/test",
            runId: "run1");

        var result = await _gate.ExecuteAsync(context);

        result.Status.Should().Be(GateStatus.Skipped);
        result.Passed.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_WhenEnabledAndScoreMeetsThreshold_ReturnsPass()
    {
        _strykerService.RunMutationTestAsync(
            Arg.Any<string>(),
            Arg.Any<TimeSpan>(),
            Arg.Any<string?>(),
            Arg.Any<string?>(),
            Arg.Any<IEnumerable<string>?>(),
            Arg.Any<CancellationToken>())
            .Returns(75.5m);

        var options = new QualityGateOptions();
        options.Mutation.Enabled = true;
        options.Mutation.MinimumScore = 70;

        var context = new QualityContext(
            QualityTarget.ForDiff(),
            affectedProjects: ["backend/Wamage.csproj"],
            testProjects: [],
            options: options,
            artifactDirectory: ".artifacts/test",
            runId: "run1");

        var result = await _gate.ExecuteAsync(context);

        result.Passed.Should().BeTrue();
        result.Status.Should().Be(GateStatus.Passed);
        result.Actual.Should().Be(75.5m);
    }

    [Fact]
    public async Task ExecuteAsync_WhenEnabledAndScoreBelowThreshold_ReturnsFail()
    {
        _strykerService.RunMutationTestAsync(
            Arg.Any<string>(),
            Arg.Any<TimeSpan>(),
            Arg.Any<string?>(),
            Arg.Any<string?>(),
            Arg.Any<IEnumerable<string>?>(),
            Arg.Any<CancellationToken>())
            .Returns(55.0m);

        var options = new QualityGateOptions();
        options.Mutation.Enabled = true;
        options.Mutation.MinimumScore = 70;

        var context = new QualityContext(
            QualityTarget.ForDiff(),
            affectedProjects: ["backend/Wamage.csproj"],
            testProjects: [],
            options: options,
            artifactDirectory: ".artifacts/test",
            runId: "run1");

        var result = await _gate.ExecuteAsync(context);

        result.Passed.Should().BeFalse();
        result.Status.Should().Be(GateStatus.Failed);
        result.Actual.Should().Be(55.0m);
    }

    [Fact]
    public void ResolveMutatePatterns_ForNamespace_ReturnsFolderGlob()
    {
        var context = new QualityContext(
            QualityTarget.ForNamespace("Wamage.Features.Persons"),
            affectedProjects: ["backend/wamage.csproj"],
            testProjects: ["backend.tests/wamage.tests.csproj"],
            options: new QualityGateOptions(),
            artifactDirectory: ".artifacts/test",
            runId: "run1");

        var patterns = MutationGate.ResolveMutatePatterns(context);

        patterns.Should().NotBeNull();
        patterns.Should().Contain("Features/Persons/**/*");
    }
}
