using System.Text.Json;
using System.Text.Json.Serialization;
using QualityGate.Domain;

namespace QualityGate.Reporting;

public sealed class JsonReporter : IReporter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public async Task ReportAsync(QualityResult result, TextWriter writer, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(writer);

        var dto = MapToDto(result);
        var json = JsonSerializer.Serialize(dto, JsonOptions);
        await writer.WriteLineAsync(json).ConfigureAwait(false);
    }

    public string Serialize(QualityResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        var dto = MapToDto(result);
        return JsonSerializer.Serialize(dto, JsonOptions);
    }

    public static QualityReportDto MapToDto(QualityResult result)
    {
        return new QualityReportDto
        {
            SchemaVersion = 1,
            Tool = new ToolDto
            {
                Name = "QualityGate",
                Version = result.ToolVersion
            },
            RunId = result.RunId,
            Passed = result.Passed,
            Scope = new ScopeDto
            {
                Type = result.Target.Scope.ToString(),
                Project = result.Target.Project,
                Namespace = result.Target.Namespace
            },
            Git = result.ChangeSet != null
                ? new GitDto
                {
                    Base = result.ChangeSet.BaseCommit,
                    Head = result.ChangeSet.HeadCommit
                }
                : null,
            DurationMs = (long)result.Duration.TotalMilliseconds,
            Gates = result.Gates.Select(g => new GateReportDto
            {
                Name = g.Gate,
                Status = g.Status.ToString(),
                Actual = g.Actual,
                Threshold = g.Threshold,
                Message = g.Message,
                DurationMs = (long)g.Duration.TotalMilliseconds,
                Findings = g.Findings.Select(f => new FindingReportDto
                {
                    Rule = f.Rule,
                    File = f.File,
                    Member = f.Member,
                    Line = f.Line,
                    Message = f.Message,
                    Severity = f.Severity,
                    Actual = f.Actual,
                    Threshold = f.Threshold
                }).ToList()
            }).ToList()
        };
    }
}

public sealed class QualityReportDto
{
    public int SchemaVersion { get; set; } = 1;
    public ToolDto Tool { get; set; } = new();
    public string RunId { get; set; } = string.Empty;
    public bool Passed { get; set; }
    public ScopeDto Scope { get; set; } = new();
    public GitDto? Git { get; set; }
    public long DurationMs { get; set; }
    public List<GateReportDto> Gates { get; set; } = [];
}

public sealed class ToolDto
{
    public string Name { get; set; } = "QualityGate";
    public string Version { get; set; } = "1.0.0";
}

public sealed class ScopeDto
{
    public string Type { get; set; } = string.Empty;
    public string? Project { get; set; }
    public string? Namespace { get; set; }
}

public sealed class GitDto
{
    public string Base { get; set; } = string.Empty;
    public string Head { get; set; } = string.Empty;
}

public sealed class GateReportDto
{
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public decimal? Actual { get; set; }
    public decimal? Threshold { get; set; }
    public string Message { get; set; } = string.Empty;
    public long DurationMs { get; set; }
    public List<FindingReportDto> Findings { get; set; } = [];
}

public sealed class FindingReportDto
{
    public string Rule { get; set; } = string.Empty;
    public string? File { get; set; }
    public string? Member { get; set; }
    public int? Line { get; set; }
    public string Message { get; set; } = string.Empty;
    public string Severity { get; set; } = "Error";
    public decimal? Actual { get; set; }
    public decimal? Threshold { get; set; }
}
