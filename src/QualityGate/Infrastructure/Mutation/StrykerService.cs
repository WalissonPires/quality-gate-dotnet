using System.Text.RegularExpressions;
using QualityGate.Infrastructure.Process;

namespace QualityGate.Infrastructure.Mutation;

public interface IStrykerService
{
    Task<decimal?> RunMutationTestAsync(
        string workingDirectory,
        TimeSpan timeout,
        string? projectPath = null,
        string? testProjectPath = null,
        IEnumerable<string>? mutatePatterns = null,
        CancellationToken cancellationToken = default);
}

public sealed class StrykerService : IStrykerService
{
    private readonly IProcessRunner _processRunner;

    public StrykerService(IProcessRunner processRunner)
    {
        _processRunner = processRunner ?? throw new ArgumentNullException(nameof(processRunner));
    }

    public async Task<decimal?> RunMutationTestAsync(
        string workingDirectory,
        TimeSpan timeout,
        string? projectPath = null,
        string? testProjectPath = null,
        IEnumerable<string>? mutatePatterns = null,
        CancellationToken cancellationToken = default)
    {
        var args = new List<string> { "stryker", "--reporter", "Progress", "--reporter", "ClearText" };
        var workDir = workingDirectory;

        if (!string.IsNullOrWhiteSpace(testProjectPath))
        {
            var fullTestPath = Path.GetFullPath(testProjectPath);
            workDir = Path.GetDirectoryName(fullTestPath) ?? workingDirectory;
        }

        if (!string.IsNullOrWhiteSpace(projectPath))
        {
            var projectName = Path.GetFileName(projectPath);
            args.Add("-p");
            args.Add(projectName);
        }

        if (mutatePatterns != null)
        {
            var patternList = mutatePatterns.Where(p => !string.IsNullOrWhiteSpace(p)).ToList();
            if (patternList.Count > 0)
            {
                var formatted = "[" + string.Join(",", patternList.Select(p => $"'{p}'")) + "]";
                args.Add("--mutate");
                args.Add(formatted);
            }
        }

        var request = new ProcessRequest(
            "dotnet",
            args,
            workDir,
            timeout);

        var result = await _processRunner.RunAsync(request, cancellationToken).ConfigureAwait(false);
        if (result.TimedOut)
        {
            throw new TimeoutException($"Mutation testing timed out after {timeout.TotalSeconds} seconds.");
        }

        return ParseMutationScore(result.StandardOutput);
    }

    public static decimal? ParseMutationScore(string output)
    {
        if (string.IsNullOrWhiteSpace(output)) return null;

        // Pattern: The final mutation score is 74.50 %
        // or Mutation score: 74.5%
        // or Your mutation score is 82.50 %
        var match = Regex.Match(output, @"mutation score\s*(?:is|:)\s*([0-9]+(?:\.[0-9]+)?)\s*%", RegexOptions.IgnoreCase);
        if (match.Success && decimal.TryParse(match.Groups[1].Value, out var score))
        {
            return score;
        }

        return null;
    }
}
