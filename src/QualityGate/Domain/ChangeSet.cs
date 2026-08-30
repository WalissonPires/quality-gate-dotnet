namespace QualityGate.Domain;

public sealed record ChangeSet
{
    public string BaseCommit { get; }
    public string HeadCommit { get; }
    public IReadOnlyCollection<ChangedFile> Files { get; }

    public ChangeSet(string baseCommit, string headCommit, IEnumerable<ChangedFile> files)
    {
        if (string.IsNullOrWhiteSpace(baseCommit))
        {
            throw new ArgumentException("Base commit cannot be null or whitespace.", nameof(baseCommit));
        }

        if (string.IsNullOrWhiteSpace(headCommit))
        {
            throw new ArgumentException("Head commit cannot be null or whitespace.", nameof(headCommit));
        }

        ArgumentNullException.ThrowIfNull(files);

        BaseCommit = baseCommit;
        HeadCommit = headCommit;
        Files = files.ToList().AsReadOnly();
    }

    public IReadOnlyCollection<ChangedFile> NonDeletedFiles =>
        Files.Where(f => f.ChangeType != ChangeType.Deleted).ToList().AsReadOnly();

    public IReadOnlyCollection<ChangedFile> CSharpSourceFiles =>
        NonDeletedFiles.Where(f => f.Path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)).ToList().AsReadOnly();
}
