using QualityGate.Domain;
using QualityGate.Infrastructure.Coverage;

namespace QualityGate.Application;

public sealed record RatchetEvaluationResult(
    bool Passed,
    IReadOnlyList<GateFinding> Findings,
    IReadOnlyList<string> Summaries);

public static class RatchetEvaluator
{
    public static RatchetEvaluationResult Evaluate(
        QualityResult currentResult,
        QualityBaseline baseline,
        CoverageSummary? coverageSummary = null,
        IReadOnlyList<string>? affectedFeatures = null)
    {
        ArgumentNullException.ThrowIfNull(currentResult);
        ArgumentNullException.ThrowIfNull(baseline);

        var findings = new List<GateFinding>();
        var summaries = new List<string>();

        bool isGlobalScope = currentResult.Target.Scope is QualityScope.Repository or QualityScope.Project;

        // 1. Evaluate Architecture Violations Ratchet (Global repository or project scope only)
        if (isGlobalScope)
        {
            var archGate = currentResult.Gates.FirstOrDefault(g => g.Gate.Equals("Architecture", StringComparison.OrdinalIgnoreCase));
            if (archGate != null && archGate.Status != GateStatus.Skipped && archGate.Status != GateStatus.NotApplicable)
            {
                int currentViolations = (int)(archGate.Actual ?? archGate.Findings.Count);
                int baselineViolations = baseline.Metrics.ArchitectureViolations;

                if (currentViolations > baselineViolations)
                {
                    int delta = currentViolations - baselineViolations;
                    var msg = $"Global architecture violations increased by {delta} (baseline: {baselineViolations}, current: {currentViolations}).";
                    findings.Add(new GateFinding("Ratchet.ArchitectureViolations", msg, "Error", actual: currentViolations, threshold: baselineViolations));
                    summaries.Add($"❌ {msg}");
                }
                else
                {
                    summaries.Add($"✅ Global architecture violations: {currentViolations} <= {baselineViolations} (baseline).");
                }
            }
        }

        // 2. Evaluate Compiler Warnings Ratchet (Global repository or project scope only)
        if (isGlobalScope)
        {
            var analysisGate = currentResult.Gates.FirstOrDefault(g => g.Gate.Equals("StaticAnalysis", StringComparison.OrdinalIgnoreCase));
            if (analysisGate != null && analysisGate.Status != GateStatus.Skipped && analysisGate.Status != GateStatus.NotApplicable)
            {
                int currentWarnings = (int)(analysisGate.Actual ?? 0);
                int baselineWarnings = baseline.Metrics.TotalWarnings;

                if (currentWarnings > baselineWarnings)
                {
                    int delta = currentWarnings - baselineWarnings;
                    var msg = $"Global compiler warnings increased by {delta} (baseline: {baselineWarnings}, current: {currentWarnings}).";
                    findings.Add(new GateFinding("Ratchet.Warnings", msg, "Error", actual: currentWarnings, threshold: baselineWarnings));
                    summaries.Add($"❌ {msg}");
                }
                else
                {
                    summaries.Add($"✅ Global compiler warnings: {currentWarnings} <= {baselineWarnings} (baseline).");
                }
            }
        }

        // 3. Evaluate Global Coverage Ratchet (Global repository or project scope only)
        if (isGlobalScope)
        {
            var coverageGate = currentResult.Gates.FirstOrDefault(g => g.Gate.Equals("Coverage", StringComparison.OrdinalIgnoreCase));
            if (coverageGate != null && coverageGate.Actual.HasValue && baseline.Metrics.LineCoverage.HasValue)
            {
                decimal currentLineCoverage = coverageGate.Actual.Value;
                decimal baselineLineCoverage = baseline.Metrics.LineCoverage.Value;

                if (currentLineCoverage < baselineLineCoverage)
                {
                    decimal delta = baselineLineCoverage - currentLineCoverage;
                    var msg = $"Global line coverage decreased by {delta:0.#}% (baseline: {baselineLineCoverage:0.#}%, current: {currentLineCoverage:0.#}%).";
                    findings.Add(new GateFinding("Ratchet.GlobalCoverage", msg, "Error", actual: currentLineCoverage, threshold: baselineLineCoverage));
                    summaries.Add($"❌ {msg}");
                }
                else
                {
                    summaries.Add($"✅ Global line coverage: {currentLineCoverage:0.#}% >= {baselineLineCoverage:0.#}% (baseline).");
                }
            }
        }

        // 4. Evaluate Per-Feature Ratchet
        if (coverageSummary != null && baseline.Features.Count > 0)
        {
            var featuresToCheck = affectedFeatures ?? baseline.Features.Keys.ToList();

            foreach (var featureName in featuresToCheck)
            {
                if (!baseline.Features.TryGetValue(featureName, out var baselineFeature))
                {
                    continue;
                }

                // Compute current coverage for this feature
                var featureReports = coverageSummary.FileReports
                    .Where(f => ProjectDiscovery.FindFeatureForFile(f.Key)?.Equals(featureName, StringComparison.OrdinalIgnoreCase) == true)
                    .Select(f => f.Value)
                    .ToList();

                if (featureReports.Count > 0)
                {
                    int featureValid = featureReports.Sum(r => r.LinesValid);
                    int featureCovered = featureReports.Sum(r => r.LinesCovered);

                    if (featureValid > 0 && baselineFeature.LineCoverage.HasValue)
                    {
                        decimal currentCoverage = Math.Round((decimal)featureCovered / featureValid * 100m, 1);
                        decimal baselineCoverage = baselineFeature.LineCoverage.Value;

                        if (currentCoverage < baselineCoverage)
                        {
                            decimal delta = baselineCoverage - currentCoverage;
                            var msg = $"Line coverage for feature '{featureName}' decreased by {delta:0.#}% (baseline: {baselineCoverage:0.#}%, current: {currentCoverage:0.#}%).";
                            findings.Add(new GateFinding("Ratchet.FeatureCoverage", msg, "Error", $"Features/{featureName}", null, null, currentCoverage, baselineCoverage));
                            summaries.Add($"❌ {msg}");
                        }
                        else
                        {
                            summaries.Add($"✅ Feature '{featureName}' coverage: {currentCoverage:0.#}% >= {baselineCoverage:0.#}% (baseline).");
                        }
                    }
                }
            }
        }

        if (!isGlobalScope && summaries.Count == 0 && findings.Count == 0)
        {
            summaries.Add("✅ Per-feature ratchet: Metrics for affected features met or exceeded baseline.");
        }

        bool passed = findings.Count == 0;
        return new RatchetEvaluationResult(passed, findings, summaries);
    }
}
