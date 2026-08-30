using FluentAssertions;
using NSubstitute;
using QualityGate.Configuration;
using QualityGate.Domain;
using QualityGate.Gates;
using QualityGate.Infrastructure.Dotnet;
using Xunit;

namespace QualityGate.Tests.Gates;

public sealed class TestGateTests
{
    private readonly IDotnetService _dotnetService = Substitute.For<IDotnetService>();
    private readonly TestGate _gate;

    public TestGateTests()
    {
        _gate = new TestGate(_dotnetService);
    }

    [Fact]
    public async Task ExecuteAsync_WhenAllTestsPass_ReturnsPass()
    {
        _dotnetService.TestAsync(
            "backend.tests/Wamage.Tests.csproj",
            Arg.Any<string?>(),
            Arg.Any<bool>(),
            Arg.Any<string>(),
            Arg.Any<bool>(),
            Arg.Any<string?>(),
            Arg.Any<string>(),
            Arg.Any<TimeSpan?>(),
            Arg.Any<CancellationToken>())
            .Returns(new DotnetTestResult(0, 10, 10, 0, 0, "Passed: 10", string.Empty, TimeSpan.FromSeconds(3), false));

        var context = new QualityContext(
            QualityTarget.ForDiff(),
            affectedProjects: ["backend/Wamage.csproj"],
            testProjects: ["backend.tests/Wamage.Tests.csproj"],
            options: new QualityGateOptions(),
            artifactDirectory: ".artifacts/test",
            runId: "run1");

        var result = await _gate.ExecuteAsync(context);

        result.Passed.Should().BeTrue();
        result.Status.Should().Be(GateStatus.Passed);
        result.Actual.Should().Be(10);
    }

    [Fact]
    public async Task ExecuteAsync_WhenTestsFail_ReturnsFailWithFindings()
    {
        var output = "Failed Wamage.Tests.PersonTests.Create_ShouldWork";
        _dotnetService.TestAsync(
            "backend.tests/Wamage.Tests.csproj",
            Arg.Any<string?>(),
            Arg.Any<bool>(),
            Arg.Any<string>(),
            Arg.Any<bool>(),
            Arg.Any<string?>(),
            Arg.Any<string>(),
            Arg.Any<TimeSpan?>(),
            Arg.Any<CancellationToken>())
            .Returns(new DotnetTestResult(1, 10, 9, 1, 0, output, string.Empty, TimeSpan.FromSeconds(3), false));

        var context = new QualityContext(
            QualityTarget.ForDiff(),
            affectedProjects: ["backend/Wamage.csproj"],
            testProjects: ["backend.tests/Wamage.Tests.csproj"],
            options: new QualityGateOptions(),
            artifactDirectory: ".artifacts/test",
            runId: "run1");

        var result = await _gate.ExecuteAsync(context);

        result.Passed.Should().BeFalse();
        result.Status.Should().Be(GateStatus.Failed);
        result.Findings.Should().ContainSingle();
        result.Findings[0].Member.Should().Be("Wamage.Tests.PersonTests.Create_ShouldWork");
    }
}
