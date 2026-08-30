using FluentAssertions;
using QualityGate.Infrastructure.Coverage;
using Xunit;

namespace QualityGate.Tests.Infrastructure;

public sealed class CoberturaParserTests
{
    [Fact]
    public void ParseXml_WithValidXml_ShouldExtractCoverageSummary()
    {
        var xml = """
        <?xml version="1.0" encoding="utf-8"?>
        <coverage line-rate="0.75" branch-rate="0.5" lines-covered="6" lines-valid="8" branches-covered="1" branches-valid="2">
          <packages>
            <package name="Wamage">
              <classes>
                <class name="Wamage.Features.Persons.Person" filename="backend/Features/Persons/Person.cs">
                  <lines>
                    <line number="10" hits="2" branch="false" />
                    <line number="11" hits="2" branch="false" />
                    <line number="12" hits="0" branch="false" />
                    <line number="13" hits="1" branch="true" condition-coverage="50% (1/2)" />
                    <line number="14" hits="0" branch="false" />
                    <line number="15" hits="1" branch="false" />
                    <line number="16" hits="1" branch="false" />
                    <line number="17" hits="1" branch="false" />
                  </lines>
                </class>
              </classes>
            </package>
          </packages>
        </coverage>
        """;

        var summary = CoberturaParser.ParseXml(xml);

        summary.TotalLines.Should().Be(8);
        summary.CoveredLines.Should().Be(6);
        summary.TotalBranches.Should().Be(1);
        summary.CoveredBranches.Should().Be(1);
        summary.LineRate.Should().Be(75.0m);
        summary.FileReports.Should().ContainKey("backend/Features/Persons/Person.cs");

        var file = summary.FileReports["backend/Features/Persons/Person.cs"];
        file.Lines[10].IsCovered.Should().BeTrue();
        file.Lines[12].IsCovered.Should().BeFalse();
        file.Lines[13].IsBranch.Should().BeTrue();
    }

    [Fact]
    public async Task ParseAsync_WithFixtureFile_ShouldLoadCorrectly()
    {
        var fixturePath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "coverage", "sample.cobertura.xml");
        if (!File.Exists(fixturePath))
        {
            fixturePath = Path.Combine(Directory.GetCurrentDirectory(), "Fixtures", "coverage", "sample.cobertura.xml");
        }

        var parser = new CoberturaParser();
        var summary = await parser.ParseAsync(fixturePath);

        summary.FileReports.Should().NotBeEmpty();
    }
}
