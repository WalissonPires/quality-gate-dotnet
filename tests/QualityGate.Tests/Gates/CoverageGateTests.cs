using FluentAssertions;
using NSubstitute;
using QualityGate.Configuration;
using QualityGate.Domain;
using QualityGate.Gates;
using QualityGate.Infrastructure.Coverage;
using Xunit;

namespace QualityGate.Tests.Gates;

public sealed class CoverageGateTests
{
    private readonly ICoverageParser _coverageParser = Substitute.For<ICoverageParser>();
    private readonly CoverageGate _gate;

    public CoverageGateTests()
    {
        _gate = new CoverageGate(_coverageParser);
    }

    [Fact]
    public async Task ExecuteAsync_WhenCoverageMeetsThreshold_ReturnsPass()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var xmlPath = Path.Combine(tempDir, "coverage.cobertura.xml");
        await File.WriteAllTextAsync(xmlPath, "<coverage />");

        var fileReport = new FileCoverageReport(
            "backend/Features/Persons/Person.cs",
            10, 9, 0, 0,
            new Dictionary<int, LineCoverageInfo>
            {
                [1] = new(1, 1),
                [2] = new(2, 1)
            });

        var summary = new CoverageSummary(
            10, 9, 0, 0,
            new Dictionary<string, FileCoverageReport>
            {
                ["backend/Features/Persons/Person.cs"] = fileReport
            });

        _coverageParser.ParseMultipleAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>())
            .Returns(summary);

        var changeSet = new ChangeSet("base", "head", [
            new ChangedFile("backend/Features/Persons/Person.cs", ChangeType.Modified)
        ]);

        var options = new QualityGateOptions();
        options.ChangedCode.LineCoverage = 80;

        var context = new QualityContext(
            QualityTarget.ForDiff(),
            affectedProjects: ["backend/Wamage.csproj"],
            testProjects: ["backend.tests/Wamage.Tests.csproj"],
            options: options,
            artifactDirectory: tempDir,
            runId: "run1",
            changeSet: changeSet);

        try
        {
            var result = await _gate.ExecuteAsync(context);

            result.Passed.Should().BeTrue();
            result.Status.Should().Be(GateStatus.Passed);
            result.Actual.Should().Be(90.0m);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task ExecuteAsync_WhenCoverageBelowThreshold_ReturnsFail()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var xmlPath = Path.Combine(tempDir, "coverage.cobertura.xml");
        await File.WriteAllTextAsync(xmlPath, "<coverage />");

        var fileReport = new FileCoverageReport(
            "backend/Features/Persons/Person.cs",
            10, 5, 0, 0,
            new Dictionary<int, LineCoverageInfo>
            {
                [1] = new(1, 1),
                [2] = new(2, 0)
            });

        var summary = new CoverageSummary(
            10, 5, 0, 0,
            new Dictionary<string, FileCoverageReport>
            {
                ["backend/Features/Persons/Person.cs"] = fileReport
            });

        _coverageParser.ParseMultipleAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>())
            .Returns(summary);

        var changeSet = new ChangeSet("base", "head", [
            new ChangedFile("backend/Features/Persons/Person.cs", ChangeType.Modified)
        ]);

        var options = new QualityGateOptions();
        options.ChangedCode.LineCoverage = 80;

        var context = new QualityContext(
            QualityTarget.ForDiff(),
            affectedProjects: ["backend/Wamage.csproj"],
            testProjects: ["backend.tests/Wamage.Tests.csproj"],
            options: options,
            artifactDirectory: tempDir,
            runId: "run1",
            changeSet: changeSet);

        try
        {
            var result = await _gate.ExecuteAsync(context);

            result.Passed.Should().BeFalse();
            result.Status.Should().Be(GateStatus.Failed);
            result.Actual.Should().Be(50.0m);
            result.Findings.Should().NotBeEmpty();
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task ExecuteAsync_WhenOnlyGeneratedFilesChanged_ReturnsNotApplicable()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var xmlPath = Path.Combine(tempDir, "coverage.cobertura.xml");
        await File.WriteAllTextAsync(xmlPath, "<coverage />");

        _coverageParser.ParseMultipleAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>())
            .Returns(new CoverageSummary(100, 0, 0, 0, new Dictionary<string, FileCoverageReport>()));

        var changeSet = new ChangeSet("base", "head", [
            new ChangedFile("Migrations/20260902_AddFeature.Designer.cs", ChangeType.Added),
            new ChangedFile("Data/AppDbContextModelSnapshot.cs", ChangeType.Modified)
        ]);

        var context = new QualityContext(
            QualityTarget.ForDiff(),
            affectedProjects: ["backend/Wamage.csproj"],
            testProjects: ["backend.tests/Wamage.Tests.csproj"],
            options: new QualityGateOptions(),
            artifactDirectory: tempDir,
            runId: "run1",
            changeSet: changeSet);

        try
        {
            var result = await _gate.ExecuteAsync(context);
            result.Status.Should().Be(GateStatus.NotApplicable);
            result.Message.Should().Contain("ignored");
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }
}
