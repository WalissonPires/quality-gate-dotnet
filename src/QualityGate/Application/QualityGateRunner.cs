using System.Diagnostics;
using QualityGate.Domain;
using QualityGate.Gates;

namespace QualityGate.Application;

public sealed class QualityGateRunner
{
    private readonly IReadOnlyList<IQualityGate> _gates;
    private readonly string _toolVersion;

    public QualityGateRunner(IEnumerable<IQualityGate> gates, string toolVersion = "1.0.0")
    {
        _gates = (gates ?? []).ToList().AsReadOnly();
        _toolVersion = toolVersion;
    }

    public async Task<QualityResult> RunAsync(
        QualityContext context,
        IReadOnlyCollection<string>? skipGates = null,
        IReadOnlyCollection<string>? onlyGates = null,
        bool? failFastOverride = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var stopwatch = Stopwatch.StartNew();
        var results = new List<GateResult>();
        bool failFast = failFastOverride ?? context.Options.Execution.FailFast;

        var skipSet = new HashSet<string>(skipGates ?? [], StringComparer.OrdinalIgnoreCase);
        var onlySet = onlyGates != null && onlyGates.Count > 0
            ? new HashSet<string>(onlyGates, StringComparer.OrdinalIgnoreCase)
            : null;

        foreach (var gate in _gates)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (skipSet.Contains(gate.Name) || (onlySet != null && !onlySet.Contains(gate.Name)))
            {
                if (context.Verbose)
                {
                    Console.WriteLine($"[Verbose] [{gate.Name}] Skipped.");
                }
                results.Add(GateResult.Skip(gate.Name, $"Gate '{gate.Name}' was skipped."));
                continue;
            }

            GateResult result;
            var gateStopwatch = Stopwatch.StartNew();
            if (context.Verbose)
            {
                Console.WriteLine($"[Verbose] [{gate.Name}] Starting evaluation...");
            }
            try
            {
                result = await gate.ExecuteAsync(context, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                gateStopwatch.Stop();
                result = GateResult.Error(
                    gate.Name,
                    $"Gate '{gate.Name}' encountered an unexpected error: {ex.Message}",
                    gateStopwatch.Elapsed,
                    [new GateFinding(gate.Name, ex.ToString(), "Error")]);
            }

            results.Add(result);
            if (context.Verbose)
            {
                Console.WriteLine($"[Verbose] [{gate.Name}] Completed in {gateStopwatch.ElapsedMilliseconds}ms with status: {result.Status} - {result.Message}");
            }

            if (failFast && !result.Passed)
            {
                break;
            }
        }

        stopwatch.Stop();
        return ResultAggregator.Aggregate(
            context.Target,
            results,
            stopwatch.Elapsed,
            _toolVersion,
            context.RunId,
            context.ChangeSet);
    }
}
