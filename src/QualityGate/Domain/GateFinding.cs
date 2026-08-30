namespace QualityGate.Domain;

public sealed record GateFinding
{
    public string Rule { get; }
    public string Message { get; }
    public string Severity { get; }
    public string? File { get; }
    public string? Member { get; }
    public int? Line { get; }
    public decimal? Actual { get; }
    public decimal? Threshold { get; }

    public GateFinding(
        string rule,
        string message,
        string severity = "Error",
        string? file = null,
        string? member = null,
        int? line = null,
        decimal? actual = null,
        decimal? threshold = null)
    {
        if (string.IsNullOrWhiteSpace(rule))
        {
            throw new ArgumentException("Rule cannot be null or whitespace.", nameof(rule));
        }

        if (string.IsNullOrWhiteSpace(message))
        {
            throw new ArgumentException("Message cannot be null or whitespace.", nameof(message));
        }

        Rule = rule;
        Message = message;
        Severity = severity;
        File = file?.Replace('\\', '/');
        Member = member;
        Line = line;
        Actual = actual;
        Threshold = threshold;
    }
}
