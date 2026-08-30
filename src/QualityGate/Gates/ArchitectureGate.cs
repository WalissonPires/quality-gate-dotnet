using QualityGate.Domain;
using QualityGate.Infrastructure.Architecture;

namespace QualityGate.Gates;

public sealed class ArchitectureGate : IQualityGate
{
    private readonly IArchitectureValidator _validator;

    public string Name => "Architecture";

    public ArchitectureGate(IArchitectureValidator validator)
    {
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
    }

    public async Task<GateResult> ExecuteAsync(QualityContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var rules = context.Options.Architecture.Rules;
        if (rules == null || rules.Count == 0)
        {
            return GateResult.NotApplicable(Name, "No architecture rules configured.");
        }

        IEnumerable<string> sourceFiles;
        if (context.Target.Scope == QualityScope.Diff && context.ChangeSet != null)
        {
            sourceFiles = context.ChangeSet.CSharpSourceFiles.Select(f => f.Path);
        }
        else
        {
            // Find all cs files in working directory or affected projects
            var rootDir = Environment.CurrentDirectory;
            sourceFiles = Directory.GetFiles(rootDir, "*.cs", SearchOption.AllDirectories)
                .Select(f => Path.GetRelativePath(rootDir, f).Replace('\\', '/'))
                .Where(rel => !rel.StartsWith("bin/", StringComparison.OrdinalIgnoreCase) &&
                              !rel.Contains("/bin/", StringComparison.OrdinalIgnoreCase) &&
                              !rel.StartsWith("obj/", StringComparison.OrdinalIgnoreCase) &&
                              !rel.Contains("/obj/", StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        var violations = await _validator.ValidateAsync(sourceFiles, rules, cancellationToken).ConfigureAwait(false);

        if (violations.Count > 0)
        {
            var findings = violations.Select(v => new GateFinding(
                v.RuleName,
                v.Message,
                "Error",
                v.FilePath,
                member: null,
                line: v.LineNumber,
                actual: violations.Count,
                threshold: 0)).ToList();

            return GateResult.Fail(
                Name,
                $"{violations.Count} architectural violation(s) found.",
                TimeSpan.Zero,
                findings,
                actual: violations.Count,
                threshold: 0);
        }

        return GateResult.Pass(
            Name,
            "No architectural violations.",
            TimeSpan.Zero,
            actual: 0,
            threshold: 0);
    }
}
