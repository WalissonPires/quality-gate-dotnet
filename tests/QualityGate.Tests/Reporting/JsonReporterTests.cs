using System.Text.Json;
using FluentAssertions;
using QualityGate.Domain;
using QualityGate.Reporting;
using Xunit;

namespace QualityGate.Tests.Reporting;

public sealed class JsonReporterTests
{
    [Fact]
    public async Task ReportAsync_OutputsValidJsonMatchingSchema()
    {
        var target = QualityTarget.ForDiff();
        var changeSet = new ChangeSet("base1234", "head5678", [new ChangedFile("src/App.cs", ChangeType.Modified)]);
        var gates = new List<GateResult>
        {
            GateResult.Pass("Build", "Build succeeded.", TimeSpan.FromSeconds(1)),
            GateResult.Fail("Coverage", "Coverage below threshold.", TimeSpan.FromSeconds(2),
                [new GateFinding("Coverage", "2 of 5 lines covered.", "Error", "src/App.cs", "Run", 10, 40, 80)],
                actual: 40, threshold: 80)
        };

        var qualityResult = new QualityResult(false, target, gates, TimeSpan.FromSeconds(3), "1.0.0", "run-1234-abcd", changeSet);

        var reporter = new JsonReporter();
        using var writer = new StringWriter();

        await reporter.ReportAsync(qualityResult, writer);

        var json = writer.ToString();
        json.Should().NotBeNullOrWhiteSpace();

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        root.GetProperty("schemaVersion").GetInt32().Should().Be(1);
        root.GetProperty("tool").GetProperty("name").GetString().Should().Be("QualityGate");
        root.GetProperty("tool").GetProperty("version").GetString().Should().Be("1.0.0");
        root.GetProperty("runId").GetString().Should().Be("run-1234-abcd");
        root.GetProperty("passed").GetBoolean().Should().BeFalse();
        root.GetProperty("scope").GetProperty("type").GetString().Should().Be("Diff");
        root.GetProperty("git").GetProperty("base").GetString().Should().Be("base1234");
        root.GetProperty("git").GetProperty("head").GetString().Should().Be("head5678");
        root.GetProperty("gates").GetArrayLength().Should().Be(2);

        var coverageGate = root.GetProperty("gates")[1];
        coverageGate.GetProperty("name").GetString().Should().Be("Coverage");
        coverageGate.GetProperty("status").GetString().Should().Be("Failed");
        coverageGate.GetProperty("actual").GetDecimal().Should().Be(40);
        coverageGate.GetProperty("threshold").GetDecimal().Should().Be(80);
        coverageGate.GetProperty("findings").GetArrayLength().Should().Be(1);
    }
}
