using System.Text.Json.Serialization;

namespace QualityGate.Domain;

public sealed record GlobalMetrics(
    [property: JsonPropertyName("lineCoverage")] decimal? LineCoverage,
    [property: JsonPropertyName("branchCoverage")] decimal? BranchCoverage,
    [property: JsonPropertyName("totalTests")] int TotalTests,
    [property: JsonPropertyName("failedTests")] int FailedTests,
    [property: JsonPropertyName("totalWarnings")] int TotalWarnings,
    [property: JsonPropertyName("architectureViolations")] int ArchitectureViolations,
    [property: JsonPropertyName("maxCyclomaticComplexity")] int MaxCyclomaticComplexity,
    [property: JsonPropertyName("mutationScore")] decimal? MutationScore = null);

public sealed record FeatureMetrics(
    [property: JsonPropertyName("lineCoverage")] decimal? LineCoverage,
    [property: JsonPropertyName("branchCoverage")] decimal? BranchCoverage,
    [property: JsonPropertyName("architectureViolations")] int ArchitectureViolations,
    [property: JsonPropertyName("maxComplexity")] int MaxComplexity,
    [property: JsonPropertyName("totalLines")] int TotalLines = 0);

public sealed record QualityBaseline
{
    [JsonPropertyName("schemaVersion")]
    public int SchemaVersion { get; init; } = 1;

    [JsonPropertyName("commit")]
    public string Commit { get; init; } = string.Empty;

    [JsonPropertyName("timestamp")]
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    [JsonPropertyName("toolVersion")]
    public string ToolVersion { get; init; } = "1.0.0";

    [JsonPropertyName("metrics")]
    public GlobalMetrics Metrics { get; init; } = new(null, null, 0, 0, 0, 0, 1);

    [JsonPropertyName("features")]
    public IReadOnlyDictionary<string, FeatureMetrics> Features { get; init; } = new Dictionary<string, FeatureMetrics>();
}
