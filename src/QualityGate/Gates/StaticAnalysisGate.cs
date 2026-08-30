using System.Text.RegularExpressions;
using QualityGate.Domain;
using QualityGate.Infrastructure.Dotnet;

namespace QualityGate.Gates;

public sealed partial class StaticAnalysisGate : IQualityGate
{
    private readonly IDotnetService _dotnetService;

    public string Name => "StaticAnalysis";

    public StaticAnalysisGate(IDotnetService dotnetService)
    {
        _dotnetService = dotnetService ?? throw new ArgumentNullException(nameof(dotnetService));
    }

    public async Task<GateResult> ExecuteAsync(QualityContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var threshold = context.Options.ChangedCode.NewWarnings ?? 0;
        var projects = context.AffectedProjects.Distinct().ToList();

        if (projects.Count == 0 && context.Target.Scope != QualityScope.Diff)
        {
            return GateResult.NotApplicable(Name, "No projects identified for static analysis.");
        }

        var allWarnings = new List<GateFinding>();
        TimeSpan totalDuration = TimeSpan.Zero;

        foreach (var project in projects)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var timeout = TimeSpan.FromSeconds(context.Options.Execution.ProcessTimeoutSeconds);
            var buildResult = await _dotnetService.BuildAsync(project, "Release", timeout, cancellationToken).ConfigureAwait(false);
            totalDuration += buildResult.Duration;

            if (buildResult.TimedOut)
            {
                return GateResult.Error(Name, $"Static analysis timed out for project '{project}'.", totalDuration);
            }

            var warnings = ParseWarnings(buildResult.StandardOutput + "\n" + buildResult.StandardError);
            allWarnings.AddRange(warnings);
        }

        // In Diff mode, filter to changed files
        if (context.Target.Scope == QualityScope.Diff && context.ChangeSet != null)
        {
            var changedPaths = new HashSet<string>(context.ChangeSet.CSharpSourceFiles.Select(f => f.Path), StringComparer.OrdinalIgnoreCase);
            allWarnings = allWarnings.Where(w => w.File != null && changedPaths.Any(cp => w.File.EndsWith(cp, StringComparison.OrdinalIgnoreCase) || cp.EndsWith(w.File, StringComparison.OrdinalIgnoreCase))).ToList();
        }

        int warningCount = allWarnings.Count;
        if (warningCount > threshold)
        {
            return GateResult.Fail(
                Name,
                $"{warningCount} new static analysis warning(s) found (threshold: {threshold}).",
                totalDuration,
                allWarnings,
                actual: warningCount,
                threshold: threshold);
        }

        return GateResult.Pass(
            Name,
            $"{warningCount} new analyzer warning(s).",
            totalDuration,
            allWarnings,
            actual: warningCount,
            threshold: threshold);
    }

    public static IReadOnlyList<GateFinding> ParseWarnings(string output)
    {
        var findings = new List<GateFinding>();
        if (string.IsNullOrWhiteSpace(output)) return findings;

        // Pattern: file.cs(line,col): warning CS1234: Message [project.csproj]
        var regex = new Regex(@"^(?<file>.*?)\((?<line>\d+),\d+\):\s*warning\s*(?<code>[A-Z0-9]+):\s*(?<msg>.*?)(?:\s*\[.*?\])?$", RegexOptions.Multiline);
        var matches = regex.Matches(output);

        foreach (Match match in matches)
        {
            var file = match.Groups["file"].Value.Trim().Replace('\\', '/');
            var lineStr = match.Groups["line"].Value.Trim();
            var code = match.Groups["code"].Value.Trim();
            var msg = match.Groups["msg"].Value.Trim();

            int? line = int.TryParse(lineStr, out var parsedLine) ? parsedLine : null;
            findings.Add(new GateFinding(code, msg, "Warning", file, null, line));
        }

        return findings;
    }
}
