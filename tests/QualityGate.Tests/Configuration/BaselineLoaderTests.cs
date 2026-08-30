using FluentAssertions;
using QualityGate.Configuration;
using QualityGate.Domain;
using Xunit;

namespace QualityGate.Tests.Configuration;

public sealed class BaselineLoaderTests
{
    [Fact]
    public void ResolvePath_WithNull_ReturnsDefaultRelativePath()
    {
        var resolved = BaselineLoader.ResolvePath(null, "C:/repos/app");
        resolved.Replace('\\', '/').Should().EndWith(".tools/quality-gate/baseline.json");
    }

    [Fact]
    public void ResolvePath_WithCustomRelativePath_ResolvesAgainstWorkingDir()
    {
        var resolved = BaselineLoader.ResolvePath("custom/base.json", "C:/repos/app");
        resolved.Replace('\\', '/').Should().EndWith("custom/base.json");
    }

    [Fact]
    public async Task SaveAsync_And_LoadAsync_RoundTripCorrectly()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"baseline-{Guid.NewGuid():N}.json");

        var baseline = new QualityBaseline
        {
            SchemaVersion = 1,
            Commit = "abc1234",
            Timestamp = DateTimeOffset.UtcNow,
            ToolVersion = "1.0.0",
            Metrics = new GlobalMetrics(85.5m, 70.0m, 100, 0, 0, 5, 12),
            Features = new Dictionary<string, FeatureMetrics>
            {
                ["Persons"] = new(90.0m, 80.0m, 0, 10, 500),
                ["Channels"] = new(75.0m, 60.0m, 2, 25, 800)
            }
        };

        try
        {
            await BaselineLoader.SaveAsync(baseline, tempFile, Environment.CurrentDirectory);
            var loaded = await BaselineLoader.LoadAsync(tempFile, Environment.CurrentDirectory);

            loaded.Should().NotBeNull();
            loaded!.Commit.Should().Be("abc1234");
            loaded.Metrics.LineCoverage.Should().Be(85.5m);
            loaded.Metrics.ArchitectureViolations.Should().Be(5);
            loaded.Features.Should().ContainKey("Persons");
            loaded.Features["Persons"].LineCoverage.Should().Be(90.0m);
            loaded.Features["Persons"].ArchitectureViolations.Should().Be(0);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public void LoadFromJson_WithInvalidSchemaVersion_ThrowsConfigurationException()
    {
        var json = """
        {
          "schemaVersion": 2,
          "commit": "abc"
        }
        """;

        var act = () => BaselineLoader.LoadFromJson(json);

        act.Should().Throw<ConfigurationException>()
            .WithMessage("*Unsupported baseline schema version 2*");
    }
}
