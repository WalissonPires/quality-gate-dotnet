using QualityGate.Domain;
using QualityGate.Infrastructure.Mutation;

namespace QualityGate.Gates;

public sealed class MutationGate : IQualityGate
{
    private readonly IStrykerService _strykerService;

    public string Name => "Mutation";

    public MutationGate(IStrykerService strykerService)
    {
        _strykerService = strykerService ?? throw new ArgumentNullException(nameof(strykerService));
    }

    public async Task<GateResult> ExecuteAsync(QualityContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (!context.Options.Mutation.Enabled)
        {
            return GateResult.Skip(Name, "Mutation testing disabled in configuration.");
        }

        var threshold = context.Options.Mutation.MinimumScore ?? 70m;
        var timeout = TimeSpan.FromSeconds(context.Options.Execution.ProcessTimeoutSeconds * 2);

        var targetProject = context.AffectedProjects.FirstOrDefault();
        var testProject = context.TestProjects.FirstOrDefault();

        var mutatePatterns = ResolveMutatePatterns(context);

        decimal? score;
        try
        {
            score = await _strykerService.RunMutationTestAsync(
                Environment.CurrentDirectory,
                timeout,
                targetProject,
                testProject,
                mutatePatterns,
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return GateResult.Error(Name, $"Mutation testing failed to execute: {ex.Message}");
        }

        if (!score.HasValue)
        {
            return GateResult.Error(Name, "Could not determine mutation score from Stryker output.");
        }

        decimal actualScore = score.Value;
        if (actualScore < threshold)
        {
            return GateResult.Fail(
                Name,
                $"Mutation score {actualScore:0.#}% is below threshold {threshold:0.#}%.",
                TimeSpan.Zero,
                [new GateFinding("MutationScore", $"Mutation score is {actualScore:0.#}% (threshold: {threshold:0.#}%).", "Error", actual: actualScore, threshold: threshold)],
                actual: actualScore,
                threshold: threshold);
        }

        return GateResult.Pass(
            Name,
            $"Mutation score is {actualScore:0.#}% (threshold: {threshold:0.#}%).",
            TimeSpan.Zero,
            actual: actualScore,
            threshold: threshold);
    }

    public static IReadOnlyList<string>? ResolveMutatePatterns(QualityContext context)
    {
        if (context.Target.Scope == QualityScope.Namespace && !string.IsNullOrWhiteSpace(context.Target.Namespace))
        {
            var parts = context.Target.Namespace.Split('.', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2)
            {
                var relativeFolder = string.Join('/', parts.Skip(1)); // Skip root namespace (e.g. Wamage)
                return [$"{relativeFolder}/**/*", $"**/{parts.Last()}/**/*"];
            }

            return [$"**/{parts.Last()}/**/*"];
        }

        if (context.Target.Scope == QualityScope.Diff && context.ChangeSet != null)
        {
            var csharpFiles = context.ChangeSet.CSharpSourceFiles;
            if (csharpFiles.Count == 0) return null;

            return csharpFiles.Select(f =>
            {
                var path = f.Path;
                if (path.StartsWith("backend/", StringComparison.OrdinalIgnoreCase))
                {
                    path = path[8..];
                }
                return path;
            }).ToList();
        }

        return null;
    }
}
