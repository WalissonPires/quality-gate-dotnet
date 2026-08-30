using System.Text.Json;
using FluentAssertions;
using QualityGate.Domain;
using QualityGate.Reporting;
using Xunit;

namespace QualityGate.Tests.Reporting;

public sealed class JsonContractTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    [Fact]
    public void QualityReportDto_MatchesContractSchema()
    {
        var jsonSample = """
        {
          "schemaVersion": 1,
          "tool": {
            "name": "QualityGate",
            "version": "1.0.0"
          },
          "runId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
          "passed": false,
          "scope": {
            "type": "Diff",
            "project": null,
            "namespace": null
          },
          "git": {
            "base": "abc1234",
            "head": "def5678"
          },
          "durationMs": 18342,
          "gates": [
            {
              "name": "Build",
              "status": "Passed",
              "actual": null,
              "threshold": null,
              "message": "Build succeeded.",
              "durationMs": 4521,
              "findings": []
            },
            {
              "name": "Coverage",
              "status": "Failed",
              "actual": 67.2,
              "threshold": 80.0,
              "message": "Changed line coverage is below threshold.",
              "durationMs": 1200,
              "findings": [
                {
                  "rule": "Coverage",
                  "file": "backend/Features/Persons/Person.cs",
                  "member": "HandleAsync",
                  "line": 42,
                  "message": "3 of 8 changed executable lines are covered.",
                  "severity": "Error",
                  "actual": 37.5,
                  "threshold": 80.0
                }
              ]
            }
          ]
        }
        """;

        var dto = JsonSerializer.Deserialize<QualityReportDto>(jsonSample, JsonOptions);

        dto.Should().NotBeNull();
        dto!.SchemaVersion.Should().Be(1);
        dto.Tool.Name.Should().Be("QualityGate");
        dto.Tool.Version.Should().Be("1.0.0");
        dto.RunId.Should().Be("a1b2c3d4-e5f6-7890-abcd-ef1234567890");
        dto.Passed.Should().BeFalse();
        dto.Scope.Type.Should().Be("Diff");
        dto.Git.Should().NotBeNull();
        dto.Git!.Base.Should().Be("abc1234");
        dto.Gates.Should().HaveCount(2);

        var coverageGate = dto.Gates[1];
        coverageGate.Name.Should().Be("Coverage");
        coverageGate.Findings.Should().ContainSingle();

        var finding = coverageGate.Findings[0];
        finding.Rule.Should().Be("Coverage");
        finding.File.Should().Be("backend/Features/Persons/Person.cs");
        finding.Member.Should().Be("HandleAsync");
        finding.Line.Should().Be(42);
        finding.Severity.Should().Be("Error");
        finding.Actual.Should().Be(37.5m);
        finding.Threshold.Should().Be(80.0m);
    }
}
