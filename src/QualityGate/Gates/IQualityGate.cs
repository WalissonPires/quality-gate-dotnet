using QualityGate.Domain;

namespace QualityGate.Gates;

public interface IQualityGate
{
    string Name { get; }
    Task<GateResult> ExecuteAsync(QualityContext context, CancellationToken cancellationToken = default);
}
