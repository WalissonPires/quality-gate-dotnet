using FluentAssertions;
using NSubstitute;
using QualityGate.Application;
using QualityGate.Configuration;
using QualityGate.Domain;
using QualityGate.Gates;
using Xunit;

namespace QualityGate.Tests.Application;

public sealed class QualityGateRunnerTests
{
    private readonly QualityContext _context = new(
        QualityTarget.ForDiff(),
        affectedProjects: ["backend/Wamage.csproj"],
        testProjects: ["backend.tests/Wamage.Tests.csproj"],
        options: new QualityGateOptions(),
        artifactDirectory: ".artifacts/test-run",
        runId: "test-run-1");

    [Fact]
    public async Task RunAsync_WhenAllGatesPass_ReturnsOverallPassed()
    {
        var gate1 = Substitute.For<IQualityGate>();
        gate1.Name.Returns("Build");
        gate1.ExecuteAsync(_context, Arg.Any<CancellationToken>())
            .Returns(GateResult.Pass("Build", "Succeeded", TimeSpan.FromSeconds(1)));

        var gate2 = Substitute.For<IQualityGate>();
        gate2.Name.Returns("Test");
        gate2.ExecuteAsync(_context, Arg.Any<CancellationToken>())
            .Returns(GateResult.Pass("Test", "Passed", TimeSpan.FromSeconds(2)));

        var runner = new QualityGateRunner([gate1, gate2]);

        var result = await runner.RunAsync(_context);

        result.Passed.Should().BeTrue();
        result.Gates.Should().HaveCount(2);
        result.Gates.Select(g => g.Gate).Should().ContainInOrder(["Build", "Test"]);
    }

    [Fact]
    public async Task RunAsync_WithFailFast_StopsAfterFirstFailure()
    {
        var gate1 = Substitute.For<IQualityGate>();
        gate1.Name.Returns("Build");
        gate1.ExecuteAsync(_context, Arg.Any<CancellationToken>())
            .Returns(GateResult.Fail("Build", "Build failed", TimeSpan.FromSeconds(1)));

        var gate2 = Substitute.For<IQualityGate>();
        gate2.Name.Returns("Test");

        var runner = new QualityGateRunner([gate1, gate2]);

        var result = await runner.RunAsync(_context, failFastOverride: true);

        result.Passed.Should().BeFalse();
        result.Gates.Should().HaveCount(1);
        await gate2.DidNotReceive().ExecuteAsync(Arg.Any<QualityContext>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunAsync_WithSkipGates_MarksGateAsSkipped()
    {
        var gate1 = Substitute.For<IQualityGate>();
        gate1.Name.Returns("Build");
        gate1.ExecuteAsync(_context, Arg.Any<CancellationToken>())
            .Returns(GateResult.Pass("Build", "Succeeded", TimeSpan.FromSeconds(1)));

        var gate2 = Substitute.For<IQualityGate>();
        gate2.Name.Returns("Mutation");

        var runner = new QualityGateRunner([gate1, gate2]);

        var result = await runner.RunAsync(_context, skipGates: ["Mutation"]);

        result.Passed.Should().BeTrue();
        result.Gates.Should().HaveCount(2);
        result.Gates.First(g => g.Gate == "Mutation").Status.Should().Be(GateStatus.Skipped);
        await gate2.DidNotReceive().ExecuteAsync(Arg.Any<QualityContext>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunAsync_WhenGateThrowsException_CatchesAndReturnsError()
    {
        var gate = Substitute.For<IQualityGate>();
        gate.Name.Returns("Build");
        gate.ExecuteAsync(_context, Arg.Any<CancellationToken>())
            .Returns<GateResult>(_ => throw new InvalidOperationException("Tool crashed"));

        var runner = new QualityGateRunner([gate]);

        var result = await runner.RunAsync(_context);

        result.Passed.Should().BeFalse();
        result.Gates.Should().HaveCount(1);
        result.Gates[0].Status.Should().Be(GateStatus.Error);
        result.Gates[0].Message.Should().Contain("unexpected error");
    }
}
