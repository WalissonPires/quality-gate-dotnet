using FluentAssertions;
using QualityGate.Domain;
using QualityGate.Reporting;
using Xunit;

namespace QualityGate.Tests.Reporting;

public sealed class MarkdownReporterTests
{
    [Fact]
    public async Task ReportAsync_OutputsExpectedMarkdownForFailedResult()
    {
        var target = QualityTarget.ForDiff();
        var changeSet = new ChangeSet("base12345678", "head87654321", [new ChangedFile("src/App.cs", ChangeType.Modified)]);
        var gates = new List<GateResult>
        {
            GateResult.Pass("Build", "Build succeeded.", TimeSpan.FromSeconds(1.5)),
            GateResult.Fail("Coverage", "Coverage below threshold.", TimeSpan.FromMilliseconds(850),
                [new GateFinding("Coverage", "2 of 5 lines covered.", "Error", "src/App.cs", "Run", 10, 40, 80)],
                actual: 40, threshold: 80)
        };

        var qualityResult = new QualityResult(false, target, gates, TimeSpan.FromSeconds(2.35), "1.0.0", "run-1234-abcd", changeSet);

        var reporter = new MarkdownReporter();
        using var writer = new StringWriter();

        await reporter.ReportAsync(qualityResult, writer);

        var markdown = writer.ToString();

        markdown.Should().Contain("# Wamage Quality Gate Report");
        markdown.Should().Contain("> **Status:** ❌ **FAILED**");
        markdown.Should().Contain("- **Tool Version:** 1.0.0");
        markdown.Should().Contain("- **Run ID:** `run-1234-abcd`");
        markdown.Should().Contain("- **Duration:** 2.35s");
        markdown.Should().Contain("- **Scope:** `Diff`");
        markdown.Should().Contain("- **Base Commit:** `base123`");
        markdown.Should().Contain("- **Head Commit:** `head876`");
        markdown.Should().Contain("## Gates Summary");
        markdown.Should().Contain("| Build | ✅ Passed | — | — | 1.50s | Build succeeded. |");
        markdown.Should().Contain("| Coverage | ❌ Failed | 40 | 80 | 850ms | Coverage below threshold. |");
        markdown.Should().Contain("## Findings");
        markdown.Should().Contain("### Coverage");
        markdown.Should().Contain("| Error | Coverage | `src/App.cs` | `Run` | 10 | 2 of 5 lines covered. |");
    }

    [Fact]
    public async Task ReportAsync_OutputsExpectedMarkdownForPassedResult()
    {
        var target = QualityTarget.ForProject("src/MyProject.csproj");
        var gates = new List<GateResult>
        {
            GateResult.Pass("Build", "Build succeeded.", TimeSpan.FromSeconds(1)),
            GateResult.Pass("Test", "All tests passed.", TimeSpan.FromSeconds(2))
        };

        var qualityResult = new QualityResult(true, target, gates, TimeSpan.FromSeconds(3), "1.0.0", "run-5678", null);

        var reporter = new MarkdownReporter();
        using var writer = new StringWriter();

        await reporter.ReportAsync(qualityResult, writer);

        var markdown = writer.ToString();

        markdown.Should().Contain("> **Status:** ✅ **PASSED**");
        markdown.Should().Contain("- **Scope:** `Project`");
        markdown.Should().Contain("- **Project:** `src/MyProject.csproj`");
        markdown.Should().NotContain("## Findings");
    }

    [Fact]
    public async Task ReportAsync_WithNullArguments_ThrowsArgumentNullException()
    {
        var reporter = new MarkdownReporter();
        var target = QualityTarget.ForRepository();
        var qualityResult = new QualityResult(true, target, [], TimeSpan.Zero, "1.0.0", "run-id");

        var act1 = () => reporter.ReportAsync(null!, new StringWriter());
        var act2 = () => reporter.ReportAsync(qualityResult, null!);

        await act1.Should().ThrowAsync<ArgumentNullException>();
        await act2.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public void Serialize_EscapesPipesAndNewlinesInTable()
    {
        var target = QualityTarget.ForRepository();
        var gates = new List<GateResult>
        {
            GateResult.Pass("StaticAnalysis", "Message with | pipe and \r\n newline", TimeSpan.FromSeconds(1),
                [new GateFinding("Rule1", "Finding | pipe \n newline", "Warning", "src/Foo.cs")])
        };

        var qualityResult = new QualityResult(true, target, gates, TimeSpan.FromSeconds(1), "1.0.0", "run-id");

        var reporter = new MarkdownReporter();
        var markdown = reporter.Serialize(qualityResult);

        markdown.Should().Contain("Message with \\| pipe and   newline");
        markdown.Should().Contain("Finding \\| pipe   newline");
    }
}
