using QualityGate.Application;

namespace QualityGate.Domain;

public sealed record QualityResult
{
    public bool Passed { get; }
    public QualityTarget Target { get; }
    public ChangeSet? ChangeSet { get; }
    public IReadOnlyList<GateResult> Gates { get; }
    public RatchetEvaluationResult? Ratchet { get; }
    public TimeSpan Duration { get; }
    public string ToolVersion { get; }
    public string RunId { get; }

    public QualityResult(
        bool passed,
        QualityTarget target,
        IEnumerable<GateResult> gates,
        TimeSpan duration,
        string toolVersion,
        string runId,
        ChangeSet? changeSet = null,
        RatchetEvaluationResult? ratchet = null)
    {
        Target = target ?? throw new ArgumentNullException(nameof(target));
        Gates = (gates ?? []).ToList().AsReadOnly();
        Ratchet = ratchet;
        Passed = passed && Gates.All(g => g.Passed) && (Ratchet == null || Ratchet.Passed);
        Duration = duration;
        ToolVersion = string.IsNullOrWhiteSpace(toolVersion) ? "1.0.0" : toolVersion;
        RunId = string.IsNullOrWhiteSpace(runId) ? Guid.NewGuid().ToString() : runId;
        ChangeSet = changeSet;
    }
}
