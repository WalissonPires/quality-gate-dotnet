using FluentAssertions;
using NSubstitute;
using QualityGate.Configuration;
using QualityGate.Domain;
using QualityGate.Gates;
using QualityGate.Infrastructure.Dotnet;
using Xunit;

namespace QualityGate.Tests.Gates;

public sealed class BuildGateTests
{
    private readonly IDotnetService _dotnetService = Substitute.For<IDotnetService>();
    private readonly BuildGate _gate;

    public BuildGateTests()
    {
        _gate = new BuildGate(_dotnetService);
    }

    [Fact]
    public async Task ExecuteAsync_WhenBuildSucceeds_ReturnsPass()
    {
        _dotnetService.BuildAsync("backend/Wamage.csproj", Arg.Any<string>(), Arg.Any<TimeSpan?>(), Arg.Any<CancellationToken>())
            .Returns(new DotnetBuildResult(0, "Build succeeded.", string.Empty, TimeSpan.FromSeconds(2), false));

        var context = new QualityContext(
            QualityTarget.ForDiff(),
            affectedProjects: ["backend/Wamage.csproj"],
            testProjects: [],
            options: new QualityGateOptions(),
            artifactDirectory: ".artifacts/test",
            runId: "run1");

        var result = await _gate.ExecuteAsync(context);

        result.Passed.Should().BeTrue();
        result.Status.Should().Be(GateStatus.Passed);
    }

    [Fact]
    public async Task ExecuteAsync_WhenBuildFails_ReturnsFailWithParsedFindings()
    {
        var buildOutput = "backend/Person.cs(10,5): error CS1002: ; expected [backend/Wamage.csproj]";
        _dotnetService.BuildAsync("backend/Wamage.csproj", Arg.Any<string>(), Arg.Any<TimeSpan?>(), Arg.Any<CancellationToken>())
            .Returns(new DotnetBuildResult(1, buildOutput, string.Empty, TimeSpan.FromSeconds(2), false));

        var context = new QualityContext(
            QualityTarget.ForDiff(),
            affectedProjects: ["backend/Wamage.csproj"],
            testProjects: [],
            options: new QualityGateOptions(),
            artifactDirectory: ".artifacts/test",
            runId: "run1");

        var result = await _gate.ExecuteAsync(context);

        result.Passed.Should().BeFalse();
        result.Status.Should().Be(GateStatus.Failed);
        result.Findings.Should().ContainSingle();
        result.Findings[0].Rule.Should().Be("CS1002");
        result.Findings[0].Line.Should().Be(10);
    }
}
