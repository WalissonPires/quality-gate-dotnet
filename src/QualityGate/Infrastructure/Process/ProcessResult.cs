namespace QualityGate.Infrastructure.Process;

public sealed record ProcessResult
{
    public int ExitCode { get; }
    public string StandardOutput { get; }
    public string StandardError { get; }
    public TimeSpan Duration { get; }
    public bool TimedOut { get; }

    public ProcessResult(int exitCode, string standardOutput, string standardError, TimeSpan duration, bool timedOut = false)
    {
        ExitCode = exitCode;
        StandardOutput = standardOutput ?? string.Empty;
        StandardError = standardError ?? string.Empty;
        Duration = duration;
        TimedOut = timedOut;
    }
}
