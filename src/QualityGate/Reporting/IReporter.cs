using QualityGate.Domain;

namespace QualityGate.Reporting;

public interface IReporter
{
    Task ReportAsync(QualityResult result, TextWriter writer, CancellationToken cancellationToken = default);
}
