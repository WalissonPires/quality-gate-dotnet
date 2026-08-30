using System.Text.Json.Serialization;

namespace QualityGate.Configuration;

public sealed class QualityGateOptions
{
    [JsonPropertyName("version")]
    public int Version { get; set; } = 1;

    [JsonPropertyName("changedCode")]
    public ChangedCodeOptions ChangedCode { get; set; } = new();

    [JsonPropertyName("global")]
    public GlobalOptions Global { get; set; } = new();

    [JsonPropertyName("mutation")]
    public MutationOptions Mutation { get; set; } = new();

    [JsonPropertyName("execution")]
    public ExecutionOptions Execution { get; set; } = new();

    [JsonPropertyName("architecture")]
    public ArchitectureOptions Architecture { get; set; } = new();
}

public sealed class ChangedCodeOptions
{
    [JsonPropertyName("lineCoverage")]
    public decimal? LineCoverage { get; set; } = 80;

    [JsonPropertyName("branchCoverage")]
    public decimal? BranchCoverage { get; set; } = 70;

    [JsonPropertyName("maxCyclomaticComplexity")]
    public int? MaxCyclomaticComplexity { get; set; } = 10;

    [JsonPropertyName("maxMethodLines")]
    public int? MaxMethodLines { get; set; } = 40;

    [JsonPropertyName("maxClassLines")]
    public int? MaxClassLines { get; set; } = 300;

    [JsonPropertyName("newWarnings")]
    public int? NewWarnings { get; set; } = 0;
}

public sealed class GlobalOptions
{
    [JsonPropertyName("lineCoverage")]
    public decimal? LineCoverage { get; set; }

    [JsonPropertyName("branchCoverage")]
    public decimal? BranchCoverage { get; set; }
}

public sealed class MutationOptions
{
    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; }

    [JsonPropertyName("minimumScore")]
    public decimal? MinimumScore { get; set; } = 70;
}

public sealed class ExecutionOptions
{
    [JsonPropertyName("failFast")]
    public bool FailFast { get; set; }

    [JsonPropertyName("unitTests")]
    public bool UnitTests { get; set; } = true;

    [JsonPropertyName("integrationTests")]
    public bool IntegrationTests { get; set; }

    [JsonPropertyName("e2eTests")]
    public bool E2eTests { get; set; }

    [JsonPropertyName("processTimeoutSeconds")]
    public int ProcessTimeoutSeconds { get; set; } = 300;
}

public sealed class ArchitectureOptions
{
    [JsonPropertyName("rules")]
    public List<ArchitectureRuleOptions> Rules { get; set; } = [];
}

public sealed class ArchitectureRuleOptions
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("source")]
    public string Source { get; set; } = string.Empty;

    [JsonPropertyName("forbiddenDependencies")]
    public List<string> ForbiddenDependencies { get; set; } = [];

    [JsonPropertyName("strict")]
    public bool Strict { get; set; } = true;
}
