using QualityGate.Configuration;

namespace QualityGate.Domain;

public sealed record QualityContext
{
    public QualityTarget Target { get; }
    public IReadOnlyCollection<string> AffectedProjects { get; }
    public IReadOnlyCollection<string> TestProjects { get; }
    public QualityGateOptions Options { get; }
    public string ArtifactDirectory { get; }
    public string RunId { get; }
    public CancellationToken CancellationToken { get; }
    public ChangeSet? ChangeSet { get; }
    public bool Verbose { get; }
    public QualityContext(
        QualityTarget target,
        IEnumerable<string> affectedProjects,
        IEnumerable<string> testProjects,
        QualityGateOptions options,
        string artifactDirectory,
        string runId,
        CancellationToken cancellationToken = default,
        ChangeSet? changeSet = null,
        bool verbose = false)
    {
        Target = target ?? throw new ArgumentNullException(nameof(target));
        AffectedProjects = (affectedProjects ?? []).ToList().AsReadOnly();
        TestProjects = (testProjects ?? []).ToList().AsReadOnly();
        Options = options ?? throw new ArgumentNullException(nameof(options));
        ArtifactDirectory = artifactDirectory ?? throw new ArgumentNullException(nameof(artifactDirectory));
        RunId = string.IsNullOrWhiteSpace(runId) ? Guid.NewGuid().ToString() : runId;
        CancellationToken = cancellationToken;
        ChangeSet = changeSet;
        Verbose = verbose;
    }
}
