using QualityGate.Domain;
using QualityGate.Infrastructure.Architecture;

namespace QualityGate.Gates;

public sealed class ArchitectureGate : IQualityGate
{
    private readonly IArchitectureValidator _validator;

    public string Name => "Architecture";

    public ArchitectureGate(IArchitectureValidator validator)
    {
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
    }

    public async Task<GateResult> ExecuteAsync(QualityContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var rules = context.Options.Architecture.Rules;
        if (rules == null || rules.Count == 0)
        {
            return GateResult.NotApplicable(Name, "No architecture rules configured.");
        }

        IEnumerable<string> sourceFiles;
        if (context.Target.Scope == QualityScope.Diff && context.ChangeSet != null)
        {
            sourceFiles = context.ChangeSet.CSharpSourceFiles.Select(f => f.Path);
        }
        else
        {
            // Find all cs files in working directory or affected projects
            var rootDir = Environment.CurrentDirectory;
            sourceFiles = Directory.GetFiles(rootDir, "*.cs", SearchOption.AllDirectories)
                .Select(f => Path.GetRelativePath(rootDir, f).Replace('\\', '/'))
                .Where(rel => !rel.StartsWith("bin/", StringComparison.OrdinalIgnoreCase) &&
                              !rel.Contains("/bin/", StringComparison.OrdinalIgnoreCase) &&
                              !rel.StartsWith("obj/", StringComparison.OrdinalIgnoreCase) &&
                              !rel.Contains("/obj/", StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        var violations = await _validator.ValidateAsync(sourceFiles, rules, cancellationToken).ConfigureAwait(false);

        if (violations.Count > 0)
        {
            if (context.Ratchet && context.Baseline != null)
            {
                var baseline = context.Baseline;
                if (context.Target.Scope == QualityScope.Diff)
                {
                    var findings = new List<GateFinding>();
                    bool hasErrors = false;
                    int totalBaselineAllowed = 0;

                    var violationsByFeature = violations.GroupBy(v =>
                    {
                        var feat = Application.ProjectDiscovery.FindFeatureForFile(v.FilePath);
                        if (string.IsNullOrWhiteSpace(feat) && baseline.Features.Count > 0)
                        {
                            feat = baseline.Features.Keys.FirstOrDefault(f => v.SourceNamespace.Contains($".{f}.", StringComparison.OrdinalIgnoreCase));
                        }
                        return feat;
                    });

                    foreach (var group in violationsByFeature)
                    {
                        var featureName = group.Key;
                        int countInFeature = group.Count();
                        int allowedInFeature = 0;

                        if (!string.IsNullOrWhiteSpace(featureName) && baseline.Features.TryGetValue(featureName, out var baselineFeat))
                        {
                            allowedInFeature = baselineFeat.ArchitectureViolations;
                        }

                        totalBaselineAllowed += allowedInFeature;

                        if (countInFeature > allowedInFeature)
                        {
                            hasErrors = true;
                            int i = 0;
                            foreach (var v in group)
                            {
                                i++;
                                string severity = i <= allowedInFeature ? "Warning" : "Error";
                                string msg = severity == "Warning"
                                    ? $"{v.Message} (Tolerated by baseline for feature '{featureName ?? "Unknown"}': {countInFeature} <= {allowedInFeature})"
                                    : $"{v.Message} (Exceeds baseline threshold of {allowedInFeature} for feature '{featureName ?? "Unknown"}')";

                                findings.Add(new GateFinding(v.RuleName, msg, severity, v.FilePath, null, v.LineNumber, countInFeature, allowedInFeature));
                            }
                        }
                        else
                        {
                            foreach (var v in group)
                            {
                                var msg = $"{v.Message} (Tolerated by baseline for feature '{featureName ?? "Unknown"}': {countInFeature} <= {allowedInFeature})";
                                findings.Add(new GateFinding(v.RuleName, msg, "Warning", v.FilePath, null, v.LineNumber, countInFeature, allowedInFeature));
                            }
                        }
                    }

                    if (hasErrors)
                    {
                        int errorCount = findings.Count(f => f.Severity.Equals("Error", StringComparison.OrdinalIgnoreCase));
                        return GateResult.Fail(
                            Name,
                            $"{errorCount} architectural violation(s) exceeded baseline thresholds in affected features.",
                            TimeSpan.Zero,
                            findings,
                            actual: violations.Count,
                            threshold: totalBaselineAllowed);
                    }

                    return GateResult.Pass(
                        Name,
                        $"{violations.Count} architectural violation(s) found (all within baseline tolerance).",
                        TimeSpan.Zero,
                        findings,
                        actual: violations.Count,
                        threshold: totalBaselineAllowed);
                }
                else
                {
                    int allowedGlobal = baseline.Metrics.ArchitectureViolations;
                    if (violations.Count > allowedGlobal)
                    {
                        var findings = violations.Select(v => new GateFinding(
                            v.RuleName,
                            $"{v.Message} (Global baseline: {allowedGlobal})",
                            "Error",
                            v.FilePath,
                            member: null,
                            line: v.LineNumber,
                            actual: violations.Count,
                            threshold: allowedGlobal)).ToList();

                        return GateResult.Fail(
                            Name,
                            $"{violations.Count} architectural violation(s) found (exceeds baseline of {allowedGlobal}).",
                            TimeSpan.Zero,
                            findings,
                            actual: violations.Count,
                            threshold: allowedGlobal);
                    }
                    else
                    {
                        var findings = violations.Select(v => new GateFinding(
                            v.RuleName,
                            $"{v.Message} (Within global baseline tolerance of {allowedGlobal})",
                            "Warning",
                            v.FilePath,
                            member: null,
                            line: v.LineNumber,
                            actual: violations.Count,
                            threshold: allowedGlobal)).ToList();

                        return GateResult.Pass(
                            Name,
                            $"{violations.Count} architectural violation(s) found (within baseline tolerance of {allowedGlobal}).",
                            TimeSpan.Zero,
                            findings,
                            actual: violations.Count,
                            threshold: allowedGlobal);
                    }
                }
            }

            var defaultFindings = violations.Select(v => new GateFinding(
                v.RuleName,
                v.Message,
                "Error",
                v.FilePath,
                member: null,
                line: v.LineNumber,
                actual: violations.Count,
                threshold: 0)).ToList();

            return GateResult.Fail(
                Name,
                $"{violations.Count} architectural violation(s) found.",
                TimeSpan.Zero,
                defaultFindings,
                actual: violations.Count,
                threshold: 0);
        }

        return GateResult.Pass(
            Name,
            "No architectural violations.",
            TimeSpan.Zero,
            actual: 0,
            threshold: 0);
    }
}
