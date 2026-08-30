using FluentAssertions;
using NSubstitute;
using QualityGate.Configuration;
using QualityGate.Domain;
using QualityGate.Gates;
using QualityGate.Infrastructure.Dotnet;
using Xunit;

namespace QualityGate.Tests.Gates;

public sealed class StaticAnalysisGateTests
{
    private readonly IDotnetService _dotnetService = Substitute.For<IDotnetService>();
    private readonly StaticAnalysisGate _gate;

    public StaticAnalysisGateTests()
    {
        _gate = new StaticAnalysisGate(_dotnetService);
    }

    [Fact]
    public void ParseWarnings_ShouldExtractWarningsFromOutput()
    {
        var output = """
        backend/Features/Persons/Person.cs(12,8): warning CS8618: Non-nullable property must contain a non-null value [backend/Wamage.csproj]
        backend/Features/Persons/Service.cs(40,1): warning CA1822: Member can be made static [backend/Wamage.csproj]
        """;

        var warnings = StaticAnalysisGate.ParseWarnings(output);

        warnings.Should().HaveCount(2);
        warnings[0].Rule.Should().Be("CS8618");
        warnings[0].File.Should().Be("backend/Features/Persons/Person.cs");
        warnings[0].Line.Should().Be(12);
        warnings[0].Severity.Should().Be("Warning");
    }

    [Fact]
    public async Task ExecuteAsync_WhenNoWarnings_ReturnsPass()
    {
        _dotnetService.BuildAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<TimeSpan?>(), Arg.Any<CancellationToken>())
            .Returns(new DotnetBuildResult(0, "Build succeeded. 0 Warning(s)", string.Empty, TimeSpan.FromSeconds(1), false));

        var context = new QualityContext(
            QualityTarget.ForDiff(),
            affectedProjects: ["backend/Wamage.csproj"],
            testProjects: [],
            options: new QualityGateOptions(),
            artifactDirectory: ".artifacts/test",
            runId: "run1",
            changeSet: new ChangeSet("base", "head", [new ChangedFile("backend/Person.cs", ChangeType.Modified)]));

        var result = await _gate.ExecuteAsync(context);

        result.Passed.Should().BeTrue();
        result.Status.Should().Be(GateStatus.Passed);
        result.Actual.Should().Be(0);
    }

    [Fact]
    public async Task ExecuteAsync_WhenNewWarningsExceedThreshold_ReturnsFail()
    {
        var buildOutput = "backend/Person.cs(12,8): warning CS8618: Non-nullable property [backend/Wamage.csproj]";
        _dotnetService.BuildAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<TimeSpan?>(), Arg.Any<CancellationToken>())
            .Returns(new DotnetBuildResult(0, buildOutput, string.Empty, TimeSpan.FromSeconds(1), false));

        var options = new QualityGateOptions();
        options.ChangedCode.NewWarnings = 0;

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
