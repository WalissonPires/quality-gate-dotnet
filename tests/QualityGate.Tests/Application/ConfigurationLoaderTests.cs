using FluentAssertions;
using QualityGate.Configuration;
using Xunit;

namespace QualityGate.Tests.Application;

public sealed class ConfigurationLoaderTests
{
    [Fact]
    public void LoadFromJson_WithValidJson_ShouldPopulateOptions()
    {
        var json = """
        {
          "version": 1,
          "changedCode": {
            "lineCoverage": 85,
            "branchCoverage": 75,
            "maxCyclomaticComplexity": 8,
            "maxMethodLines": 30,
            "maxClassLines": 250,
            "newWarnings": 0
          },
          "execution": {
            "failFast": true,
            "processTimeoutSeconds": 120
          }
        }
        """;

        var options = ConfigurationLoader.LoadFromJson(json);

        options.Version.Should().Be(1);
        options.ChangedCode.LineCoverage.Should().Be(85);
        options.ChangedCode.BranchCoverage.Should().Be(75);
        options.ChangedCode.MaxCyclomaticComplexity.Should().Be(8);
        options.ChangedCode.MaxMethodLines.Should().Be(30);
        options.ChangedCode.MaxClassLines.Should().Be(250);
        options.Execution.FailFast.Should().BeTrue();
        options.Execution.ProcessTimeoutSeconds.Should().Be(120);
    }

    [Fact]
    public void LoadFromJson_WithUnsupportedVersion_ShouldThrow()
    {
        var json = """{ "version": 2 }""";

        var act = () => ConfigurationLoader.LoadFromJson(json);

        act.Should().Throw<ConfigurationException>()
            .WithMessage("*Unsupported configuration schema version 2*");
    }

    [Fact]
    public void LoadFromJson_WithMalformedJson_ShouldThrow()
    {
        var json = """{ "version": 1, "changedCode": { """;

        var act = () => ConfigurationLoader.LoadFromJson(json);

        act.Should().Throw<ConfigurationException>()
            .WithMessage("*Malformed JSON*");
    }

    [Theory]
    [InlineData(101)]
    [InlineData(-5)]
    public void LoadFromJson_WithInvalidCoveragePercentage_ShouldThrow(decimal invalidCoverage)
    {
        var json = $$"""
        {
          "version": 1,
          "changedCode": {
            "lineCoverage": {{invalidCoverage}}
          }
        }
        """;

        var act = () => ConfigurationLoader.LoadFromJson(json);

        act.Should().Throw<ConfigurationException>()
            .WithMessage("*must be between 0 and 100*");
    }

    [Fact]
    public async Task LoadAsync_WithMissingFile_ShouldThrow()
    {
        var act = async () => await ConfigurationLoader.LoadAsync("non_existent_file.json");

        await act.Should().ThrowAsync<ConfigurationException>()
            .WithMessage("*Configuration file not found*");
    }
}
