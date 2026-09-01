using QualityGate.Domain;

namespace QualityGate.Reporting;

public sealed class ConsoleReporter : IReporter
{
    private readonly IReadOnlyList<string> _reportArtifactPaths;

    public ConsoleReporter(string? reportArtifactPath = null)
        : this(string.IsNullOrWhiteSpace(reportArtifactPath) ? [] : [reportArtifactPath])
    {
    }

    public ConsoleReporter(IEnumerable<string> reportArtifactPaths)
    {
        _reportArtifactPaths = (reportArtifactPaths ?? []).Where(p => !string.IsNullOrWhiteSpace(p)).ToList().AsReadOnly();
    }

    public async Task ReportAsync(QualityResult result, TextWriter writer, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(writer);

        var runIdShort = result.RunId.Length > 8 ? result.RunId[..8] : result.RunId;
        await writer.WriteLineAsync($"Wamage Quality Gate v{result.ToolVersion}").ConfigureAwait(false);
        await writer.WriteLineAsync($"Run: {runIdShort}").ConfigureAwait(false);
        await writer.WriteLineAsync().ConfigureAwait(false);

        await writer.WriteLineAsync("Scope").ConfigureAwait(false);
        await writer.WriteLineAsync($"  Type: {result.Target.Scope}").ConfigureAwait(false);
        if (result.Target.Scope == QualityScope.Project && !string.IsNullOrWhiteSpace(result.Target.Project))
        {
            await writer.WriteLineAsync($"  Project: {result.Target.Project}").ConfigureAwait(false);
        }
        else if (result.Target.Scope == QualityScope.Namespace && !string.IsNullOrWhiteSpace(result.Target.Namespace))
        {
            await writer.WriteLineAsync($"  Namespace: {result.Target.Namespace}").ConfigureAwait(false);
        }
        else if (result.Target.Scope == QualityScope.Diff && result.ChangeSet != null)
        {
            var baseShort = result.ChangeSet.BaseCommit.Length > 7 ? result.ChangeSet.BaseCommit[..7] : result.ChangeSet.BaseCommit;
            var headShort = result.ChangeSet.HeadCommit.Length > 7 ? result.ChangeSet.HeadCommit[..7] : result.ChangeSet.HeadCommit;
            await writer.WriteLineAsync($"  Base: {baseShort}").ConfigureAwait(false);
            await writer.WriteLineAsync($"  Head: {headShort}").ConfigureAwait(false);
        }

        await writer.WriteLineAsync().ConfigureAwait(false);

        foreach (var gate in result.Gates)
        {
            await writer.WriteLineAsync(gate.Gate.ToUpperInvariant()).ConfigureAwait(false);
            await writer.WriteLineAsync($"  {FormatGateSummary(gate)}").ConfigureAwait(false);

            if (gate.Findings.Count > 0)
            {
                var groupedByFile = gate.Findings.GroupBy(f => f.File ?? string.Empty);
                foreach (var group in groupedByFile)
                {
                    if (!string.IsNullOrEmpty(group.Key))
                    {
                        await writer.WriteLineAsync($"    {group.Key}").ConfigureAwait(false);
                    }

                    foreach (var finding in group)
                    {
                        var prefix = string.IsNullOrEmpty(group.Key) ? "    " : "      ";
                        var memberPrefix = !string.IsNullOrEmpty(finding.Member) ? $"{finding.Member}: " : string.Empty;
                        await writer.WriteLineAsync($"{prefix}{memberPrefix}{finding.Message}").ConfigureAwait(false);
                    }
                }
            }

            await writer.WriteLineAsync().ConfigureAwait(false);
        }

        string overallStatus = result.Passed ? "PASS" : "FAIL";
        await writer.WriteLineAsync($"QUALITY GATE: {overallStatus}").ConfigureAwait(false);

        if (_reportArtifactPaths.Count == 1)
        {
            await writer.WriteLineAsync($"  Full report: {_reportArtifactPaths[0]}").ConfigureAwait(false);
        }
        else if (_reportArtifactPaths.Count > 1)
        {
            await writer.WriteLineAsync("  Full reports:").ConfigureAwait(false);
            foreach (var path in _reportArtifactPaths)
            {
                await writer.WriteLineAsync($"    - {path}").ConfigureAwait(false);
            }
        }
    }

    private static string FormatGateSummary(GateResult gate)
    {
        return gate.Status switch
        {
            GateStatus.Passed => gate.Actual.HasValue
                ? $"PASS ({gate.Actual.Value:0.#})"
                : $"PASS - {gate.Message}",
            GateStatus.Failed => $"FAIL - {gate.Message}",
            GateStatus.Error => $"ERROR - {gate.Message}",
            GateStatus.Skipped => $"SKIPPED ({gate.Message})",
            GateStatus.NotApplicable => $"NOT APPLICABLE ({gate.Message})",
            _ => gate.Message
        };
    }
}
