namespace QualityGate.Domain;

public sealed record ChangedFile
{
    public string Path { get; }
    public ChangeType ChangeType { get; }
    public string? OldPath { get; }

    public ChangedFile(string path, ChangeType changeType, string? oldPath = null)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("File path cannot be null or whitespace.", nameof(path));
        }

        if (changeType == ChangeType.Renamed && string.IsNullOrWhiteSpace(oldPath))
        {
            throw new ArgumentException("OldPath cannot be null or whitespace when ChangeType is Renamed.", nameof(oldPath));
        }

        Path = path.Replace('\\', '/');
        ChangeType = changeType;
        OldPath = oldPath?.Replace('\\', '/');
    }
}
