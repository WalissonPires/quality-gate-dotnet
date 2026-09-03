using FluentAssertions;
using NSubstitute;
using QualityGate.Configuration;
using QualityGate.Domain;
using QualityGate.Gates;
using QualityGate.Infrastructure.Roslyn;
using Xunit;

namespace QualityGate.Tests.Gates;

public sealed class ComplexityGateTests
{
    private readonly IComplexityAnalyzer _analyzer = Substitute.For<IComplexityAnalyzer>();
    private readonly ComplexityGate _gate;

    public ComplexityGateTests()
    {
        _gate = new ComplexityGate(_analyzer);
    }

    [Fact]
    public async Task ExecuteAsync_WhenComplexityWithinThreshold_ReturnsPass()
    {
        var analysis = new ComplexityAnalysisResult(
            [new MethodComplexity("HandleAsync", "Handler", "backend/Handler.cs", 10, 5, 20)],
            [new ClassComplexity("Handler", "backend/Handler.cs", 5, 50)]);

        _analyzer.AnalyzeFilesAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>())
            .Returns(analysis);

        var context = new QualityContext(
            QualityTarget.ForDiff(),
            affectedProjects: ["backend/Wamage.csproj"],
            testProjects: [],
            options: new QualityGateOptions(),
            artifactDirectory: ".artifacts/test",
            runId: "run1",
            changeSet: new ChangeSet("base", "head", [new ChangedFile("backend/Handler.cs", ChangeType.Modified)]));

        var result = await _gate.ExecuteAsync(context);

        result.Passed.Should().BeTrue();
        result.Status.Should().Be(GateStatus.Passed);
        result.Actual.Should().Be(5);
    }

    [Fact]
    public async Task ExecuteAsync_WhenComplexityExceedsThreshold_ReturnsFail()
    {
        var analysis = new ComplexityAnalysisResult(
            [new MethodComplexity("HandleAsync", "Handler", "backend/Handler.cs", 10, 15, 20)],
            [new ClassComplexity("Handler", "backend/Handler.cs", 5, 50)]);

        _analyzer.AnalyzeFilesAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>())
            .Returns(analysis);

        var options = new QualityGateOptions();
        options.ChangedCode.MaxCyclomaticComplexity = 10;

        var context = new QualityContext(
            QualityTarget.ForDiff(),
            affectedProjects: ["backend/Wamage.csproj"],
            testProjects: [],
            options: options,
            artifactDirectory: ".artifacts/test",
            runId: "run1",
            changeSet: new ChangeSet("base", "head", [new ChangedFile("backend/Handler.cs", ChangeType.Modified)]));

        var result = await _gate.ExecuteAsync(context);

        result.Passed.Should().BeFalse();
        result.Status.Should().Be(GateStatus.Failed);
        result.Actual.Should().Be(15);
        result.Findings.Should().ContainSingle();
        result.Findings[0].Rule.Should().Be("CyclomaticComplexity");
    }

    [Fact]
    public async Task ExecuteAsync_WhenFilesBelongToTestProject_ExemptsThemFromComplexityAnalysis()
    {
        _analyzer.AnalyzeFilesAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>())
            .Returns(new ComplexityAnalysisResult([], []));

        var context = new QualityContext(
            QualityTarget.ForDiff(),
            affectedProjects: [],
            testProjects: ["backend.tests/Wamage.Tests.csproj"],
            options: new QualityGateOptions(),
            artifactDirectory: ".artifacts/test",
            runId: "run1",
            changeSet: new ChangeSet("base", "head", [
                new ChangedFile("backend.tests/IntegrationTests/WorkflowTests.cs", ChangeType.Modified)
            ]));

        var result = await _gate.ExecuteAsync(context);

        result.Passed.Should().BeTrue();
        await _analyzer.Received(1).AnalyzeFilesAsync(
            Arg.Is<IEnumerable<string>>(files => !files.Any()),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenFilesAreGenerated_IgnoresThem()
    {
        _analyzer.AnalyzeFilesAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>())
            .Returns(new ComplexityAnalysisResult([], []));

        var context = new QualityContext(
            QualityTarget.ForDiff(),
            affectedProjects: ["backend/Wamage.csproj"],
            testProjects: [],
            options: new QualityGateOptions(),
            artifactDirectory: ".artifacts/test",
            runId: "run1",
            changeSet: new ChangeSet("base", "head", [
                new ChangedFile("backend/Migrations/2026_Init.Designer.cs", ChangeType.Added),
                new ChangedFile("backend/Data/AppDbContextModelSnapshot.cs", ChangeType.Modified)
            ]));

        var result = await _gate.ExecuteAsync(context);

        result.Passed.Should().BeTrue();
        await _analyzer.Received(1).AnalyzeFilesAsync(
            Arg.Is<IEnumerable<string>>(files => !files.Any()),
            Arg.Any<CancellationToken>());
    }
}
