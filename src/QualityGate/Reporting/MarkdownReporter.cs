using System.Globalization;
using System.Text;
using QualityGate.Domain;

namespace QualityGate.Reporting;

public sealed class MarkdownReporter : IReporter
{
    public async Task ReportAsync(QualityResult result, TextWriter writer, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(writer);

        var markdown = Serialize(result);
        await writer.WriteAsync(markdown.AsMemory(), cancellationToken).ConfigureAwait(false);
    }

    public string Serialize(QualityResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        var sb = new StringBuilder();

        // Header
        sb.AppendLine("# Wamage Quality Gate Report");
        sb.AppendLine();

        var statusBadge = result.Passed ? "✅ **PASSED**" : "❌ **FAILED**";
        sb.AppendLine($"> **Status:** {statusBadge}");
        sb.AppendLine();

        // Metadata
        sb.AppendLine("## Execution Summary");
        sb.AppendLine();
        sb.AppendLine($"- **Tool Version:** {result.ToolVersion}");
        sb.AppendLine($"- **Run ID:** `{result.RunId}`");
        sb.AppendLine($"- **Duration:** {FormatDuration(result.Duration)}");
        sb.AppendLine($"- **Scope:** `{result.Target.Scope}`");

        if (result.Target.Scope == QualityScope.Project && !string.IsNullOrWhiteSpace(result.Target.Project))
        {
            sb.AppendLine($"- **Project:** `{result.Target.Project}`");
        }
        else if (result.Target.Scope == QualityScope.Namespace && !string.IsNullOrWhiteSpace(result.Target.Namespace))
        {
            sb.AppendLine($"- **Namespace:** `{result.Target.Namespace}`");
        }
        else if (result.Target.Scope == QualityScope.Diff && result.ChangeSet != null)
        {
            var baseCommit = result.ChangeSet.BaseCommit;
            var headCommit = result.ChangeSet.HeadCommit;
            var baseShort = baseCommit.Length > 7 ? baseCommit[..7] : baseCommit;
            var headShort = headCommit.Length > 7 ? headCommit[..7] : headCommit;
            sb.AppendLine($"- **Base Commit:** `{baseShort}`");
            sb.AppendLine($"- **Head Commit:** `{headShort}`");
        }

        sb.AppendLine();

        // Gates Summary Table
        sb.AppendLine("## Gates Summary");
        sb.AppendLine();
        sb.AppendLine("| Gate | Status | Actual | Threshold | Duration | Message |");
        sb.AppendLine("| :--- | :---: | :---: | :---: | :---: | :--- |");

        foreach (var gate in result.Gates)
        {
            var statusIcon = FormatStatus(gate.Status);
            var actual = gate.Actual.HasValue ? gate.Actual.Value.ToString("0.#", CultureInfo.InvariantCulture) : "—";
            var threshold = gate.Threshold.HasValue ? gate.Threshold.Value.ToString("0.#", CultureInfo.InvariantCulture) : "—";
            var duration = FormatDuration(gate.Duration);
            var message = EscapeMarkdownTable(gate.Message);

            sb.AppendLine($"| {gate.Gate} | {statusIcon} | {actual} | {threshold} | {duration} | {message} |");
        }

        sb.AppendLine();

        // Findings Details
        var gatesWithFindings = result.Gates.Where(g => g.Findings.Count > 0).ToList();
        if (gatesWithFindings.Count > 0)
        {
            sb.AppendLine("## Findings");
            sb.AppendLine();

            foreach (var gate in gatesWithFindings)
            {
                sb.AppendLine($"### {gate.Gate}");
                sb.AppendLine();
                sb.AppendLine("| Severity | Rule | File | Member | Line | Message |");
                sb.AppendLine("| :---: | :--- | :--- | :--- | :---: | :--- |");

                foreach (var finding in gate.Findings)
                {
                    var severity = finding.Severity;
                    var rule = finding.Rule;
                    var file = !string.IsNullOrEmpty(finding.File) ? $"`{finding.File}`" : "—";
                    var member = !string.IsNullOrEmpty(finding.Member) ? $"`{finding.Member}`" : "—";
                    var line = finding.Line.HasValue ? finding.Line.Value.ToString(CultureInfo.InvariantCulture) : "—";
                    var message = EscapeMarkdownTable(finding.Message);

                    sb.AppendLine($"| {severity} | {rule} | {file} | {member} | {line} | {message} |");
                }

                sb.AppendLine();
            }
        }

        return sb.ToString();
    }

    private static string FormatStatus(GateStatus status) => status switch
    {
        GateStatus.Passed => "✅ Passed",
        GateStatus.Failed => "❌ Failed",
        GateStatus.Error => "💥 Error",
        GateStatus.Skipped => "⏭️ Skipped",
        GateStatus.NotApplicable => "⚪ N/A",
        _ => status.ToString()
    };

    private static string FormatDuration(TimeSpan duration)
    {
        if (duration.TotalSeconds >= 1.0)
        {
            return duration.TotalSeconds.ToString("F2", CultureInfo.InvariantCulture) + "s";
        }

        return duration.TotalMilliseconds.ToString("F0", CultureInfo.InvariantCulture) + "ms";
    }

    private static string EscapeMarkdownTable(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        return text
            .Replace("|", "\\|")
            .Replace("\r\n", " ")
            .Replace("\n", " ")
            .Replace("\r", " ");
    }
}
