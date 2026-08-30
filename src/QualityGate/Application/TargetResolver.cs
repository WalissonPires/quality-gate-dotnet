using QualityGate.Configuration;
using QualityGate.Domain;
using QualityGate.Infrastructure.Git;

namespace QualityGate.Application;

public sealed class TargetResolver
{
    private readonly IGitService _gitService;
    private readonly string _workingDirectory;

    public TargetResolver(IGitService gitService, string? workingDirectory = null)
    {
        _gitService = gitService ?? throw new ArgumentNullException(nameof(gitService));
        _workingDirectory = string.IsNullOrWhiteSpace(workingDirectory) ? Environment.CurrentDirectory : Path.GetFullPath(workingDirectory);
    }

    public async Task<QualityTarget> ResolveAsync(
        bool diffFlag,
        string? namespaceOption,
        string? projectOption,
        bool repositoryFlag,
        CancellationToken cancellationToken = default)
    {
        int scopeCount = (diffFlag ? 1 : 0) +
                         (!string.IsNullOrWhiteSpace(namespaceOption) ? 1 : 0) +
                         (!string.IsNullOrWhiteSpace(projectOption) ? 1 : 0) +
                         (repositoryFlag ? 1 : 0);

        if (scopeCount > 1)
        {
            throw new ConfigurationException("Scope options (--diff, --namespace, --project, --repository) are mutually exclusive. Please specify only one.");
        }

        if (repositoryFlag)
        {
            return QualityTarget.ForRepository();
        }

        if (!string.IsNullOrWhiteSpace(projectOption))
        {
            var resolvedProjectPath = Path.IsPathRooted(projectOption)
                ? projectOption
                : Path.Combine(_workingDirectory, projectOption);

            if (!File.Exists(resolvedProjectPath))
            {
                throw new ScopeException($"Project file not found at: '{projectOption}'");
            }

            return QualityTarget.ForProject(projectOption);
        }

        if (!string.IsNullOrWhiteSpace(namespaceOption))
        {
            return QualityTarget.ForNamespace(namespaceOption.Trim());
        }

        if (diffFlag)
        {
            var isGit = await _gitService.IsGitRepositoryAsync(_workingDirectory, cancellationToken).ConfigureAwait(false);
            if (!isGit)
            {
                throw new ScopeException("Scope is set to Diff, but the working directory is not a valid Git repository.");
            }

            return QualityTarget.ForDiff();
        }

        // Auto-detection
        var isGitRepo = await _gitService.IsGitRepositoryAsync(_workingDirectory, cancellationToken).ConfigureAwait(false);
        if (isGitRepo)
        {
            return QualityTarget.ForDiff();
        }

        throw new ScopeException("No scope specified and not inside a Git repository. Specify --project, --namespace, or --repository.");
    }

    public static bool ShouldExpandToProjectScope(ChangeSet changeSet)
    {
        ArgumentNullException.ThrowIfNull(changeSet);

        // Check if structural files changed (Directory.Build.props, *.sln, *.props, *.targets)
        return changeSet.NonDeletedFiles.Any(f =>
            f.Path.EndsWith(".props", StringComparison.OrdinalIgnoreCase) ||
            f.Path.EndsWith(".targets", StringComparison.OrdinalIgnoreCase) ||
            f.Path.EndsWith(".sln", StringComparison.OrdinalIgnoreCase) ||
            f.Path.EndsWith(".slnx", StringComparison.OrdinalIgnoreCase));
    }
}
