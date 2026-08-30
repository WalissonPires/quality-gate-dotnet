using System.Text.RegularExpressions;
using QualityGate.Domain;
using QualityGate.Infrastructure.Dotnet;

namespace QualityGate.Gates;

public sealed partial class BuildGate : IQualityGate
{
    private readonly IDotnetService _dotnetService;

    public string Name => "Build";

    public BuildGate(IDotnetService dotnetService)
    {
        _dotnetService = dotnetService ?? throw new ArgumentNullException(nameof(dotnetService));
    }

    public async Task<GateResult> ExecuteAsync(QualityContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var projectsToBuild = context.AffectedProjects.Concat(context.TestProjects).Distinct().ToList();
        if (projectsToBuild.Count == 0 && context.Target.Scope != QualityScope.Diff)
        {
            return GateResult.NotApplicable(Name, "No projects identified to build.");
        }

        var findings = new List<GateFinding>();
        TimeSpan totalDuration = TimeSpan.Zero;

        foreach (var project in projectsToBuild)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var timeout = TimeSpan.FromSeconds(context.Options.Execution.ProcessTimeoutSeconds);
            var buildResult = await _dotnetService.BuildAsync(project, "Release", timeout, cancellationToken).ConfigureAwait(false);
            totalDuration += buildResult.Duration;

            if (buildResult.TimedOut)
            {
                return GateResult.Error(Name, $"Build timed out for project '{project}' after {timeout.TotalSeconds} seconds.", totalDuration);
            }

            if (!buildResult.Succeeded)
            {
                var errorFindings = ParseBuildErrors(buildResult.StandardOutput + "\n" + buildResult.StandardError, project);
                findings.AddRange(errorFindings);

                return GateResult.Fail(
                    Name,
                    $"Build failed for project '{project}'.",
                    totalDuration,
                    findings);
            }
        }

        return GateResult.Pass(Name, "Build succeeded.", totalDuration);
    }

    public static IReadOnlyList<GateFinding> ParseBuildErrors(string output, string fallbackProject)
    {
        var findings = new List<GateFinding>();
        if (string.IsNullOrWhiteSpace(output))
        {
            findings.Add(new GateFinding("BuildError", "Build failed with non-zero exit code.", "Error", fallbackProject));
            return findings;
        }

        // Pattern: path/to/file.cs(line,col): error CS1234: Message [path/to/project.csproj]
        var regex = new Regex(@"^(?<file>.*?)\((?<line>\d+),\d+\):\s*error\s*(?<code>[A-Z0-9]+):\s*(?<msg>.*?)(?:\s*\[.*?\])?$", RegexOptions.Multiline);
        var matches = regex.Matches(output);

        foreach (Match match in matches)
        {
            var file = match.Groups["file"].Value.Trim();
            var lineStr = match.Groups["line"].Value.Trim();
            var code = match.Groups["code"].Value.Trim();
            var msg = match.Groups["msg"].Value.Trim();

            int? line = int.TryParse(lineStr, out var parsedLine) ? parsedLine : null;
            findings.Add(new GateFinding(code, msg, "Error", file, null, line));
        }

        if (findings.Count == 0)
        {
            findings.Add(new GateFinding("BuildError", "Build failed. Check build output for details.", "Error", fallbackProject));
        }

        return findings;
    }
}
