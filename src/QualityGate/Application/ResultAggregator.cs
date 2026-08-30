using QualityGate.Domain;

namespace QualityGate.Application;

public static class ResultAggregator
{
    public static QualityResult Aggregate(
        QualityTarget target,
        IReadOnlyCollection<GateResult> gateResults,
        TimeSpan duration,
        string toolVersion,
        string runId,
        ChangeSet? changeSet = null)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(gateResults);

        bool allPassed = gateResults.All(g => g.Passed);

        return new QualityResult(
            allPassed,
            target,
            gateResults,
            duration,
            toolVersion,
            runId,
            changeSet);
    }
}
