namespace QualityGate.Infrastructure.Process;

public sealed record ProcessRequest
{
    public string FileName { get; }
    public IReadOnlyList<string> Arguments { get; }
    public string WorkingDirectory { get; }
    public TimeSpan Timeout { get; }
    public IDictionary<string, string>? EnvironmentVariables { get; }

    public ProcessRequest(
        string fileName,
        IEnumerable<string> arguments,
        string workingDirectory,
        TimeSpan timeout,
        IDictionary<string, string>? environmentVariables = null)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            throw new ArgumentException("FileName cannot be null or whitespace.", nameof(fileName));
        }

        if (timeout <= TimeSpan.Zero)
        {
            throw new ArgumentException("Timeout must be greater than zero.", nameof(timeout));
        }

        FileName = fileName;
        Arguments = (arguments ?? []).ToList().AsReadOnly();
        WorkingDirectory = string.IsNullOrWhiteSpace(workingDirectory) ? Environment.CurrentDirectory : workingDirectory;
        Timeout = timeout;
        EnvironmentVariables = environmentVariables;
    }
}
