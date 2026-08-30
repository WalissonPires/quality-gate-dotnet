namespace QualityGate.Domain;

public sealed record GateResult
{
    public string Gate { get; }
    public GateStatus Status { get; }
    public bool Passed => Status is GateStatus.Passed or GateStatus.Skipped or GateStatus.NotApplicable;
    public decimal? Actual { get; }
    public decimal? Threshold { get; }
    public string Message { get; }
    public TimeSpan Duration { get; }
    public IReadOnlyList<GateFinding> Findings { get; }
    public GateResult(
        string gate,
        GateStatus status,
        string message,
        TimeSpan duration,
        IEnumerable<GateFinding>? findings = null,
        decimal? actual = null,
        decimal? threshold = null)
    {
        if (string.IsNullOrWhiteSpace(gate))
        {
            throw new ArgumentException("Gate name cannot be null or whitespace.", nameof(gate));
        }

        if (string.IsNullOrWhiteSpace(message))
        {
            throw new ArgumentException("Message cannot be null or whitespace.", nameof(message));
        }

        Gate = gate;
        Status = status;
        Message = message;
        Duration = duration;
        Findings = (findings ?? []).ToList().AsReadOnly();
        Actual = actual;
        Threshold = threshold;
    }

    public static GateResult Pass(string gate, string message, TimeSpan duration, IEnumerable<GateFinding>? findings = null, decimal? actual = null, decimal? threshold = null) =>
        new(gate, GateStatus.Passed, message, duration, findings, actual, threshold);

    public static GateResult Fail(string gate, string message, TimeSpan duration, IEnumerable<GateFinding>? findings = null, decimal? actual = null, decimal? threshold = null) =>
        new(gate, GateStatus.Failed, message, duration, findings, actual, threshold);
    public static GateResult Error(string gate, string message, TimeSpan? duration = null, IEnumerable<GateFinding>? findings = null) =>
        new(gate, GateStatus.Error, message, duration ?? TimeSpan.Zero, findings);

    public static GateResult Skip(string gate, string message, TimeSpan? duration = null) =>
        new(gate, GateStatus.Skipped, message, duration ?? TimeSpan.Zero);

    public static GateResult NotApplicable(string gate, string message, TimeSpan? duration = null) =>
        new(gate, GateStatus.NotApplicable, message, duration ?? TimeSpan.Zero);
}
