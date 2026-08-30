using QualityGate.Domain;
using QualityGate.Infrastructure.Process;

namespace QualityGate.Infrastructure.Git;

public sealed class GitService : IGitService
{
    private readonly IProcessRunner _processRunner;
    private static readonly TimeSpan GitTimeout = TimeSpan.FromSeconds(30);

    public GitService(IProcessRunner processRunner)
    {
        _processRunner = processRunner ?? throw new ArgumentNullException(nameof(processRunner));
    }

    public async Task<bool> IsGitRepositoryAsync(string workingDirectory, CancellationToken cancellationToken = default)
    {
        var request = new ProcessRequest("git", ["rev-parse", "--is-inside-work-tree"], workingDirectory, GitTimeout);
        var result = await _processRunner.RunAsync(request, cancellationToken).ConfigureAwait(false);
        return result.ExitCode == 0 && result.StandardOutput.Trim().Equals("true", StringComparison.OrdinalIgnoreCase);
    }

    public async Task<string> GetHeadCommitAsync(string workingDirectory, CancellationToken cancellationToken = default)
    {
        var request = new ProcessRequest("git", ["rev-parse", "HEAD"], workingDirectory, GitTimeout);
        var result = await _processRunner.RunAsync(request, cancellationToken).ConfigureAwait(false);
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException($"Failed to get HEAD commit: {result.StandardError.Trim()}");
        }
        return result.StandardOutput.Trim();
    }

    public async Task<string?> GetMergeBaseAsync(string workingDirectory, string targetRef = "origin/main", CancellationToken cancellationToken = default)
    {
        var request = new ProcessRequest("git", ["merge-base", "HEAD", targetRef], workingDirectory, GitTimeout);
        var result = await _processRunner.RunAsync(request, cancellationToken).ConfigureAwait(false);
        return result.ExitCode == 0 ? result.StandardOutput.Trim() : null;
    }

    public async Task<string> ResolveBaseCommitAsync(string workingDirectory, string? explicitBase = null, CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(explicitBase))
        {
            var verifyRequest = new ProcessRequest("git", ["rev-parse", explicitBase], workingDirectory, GitTimeout);
            var verifyResult = await _processRunner.RunAsync(verifyRequest, cancellationToken).ConfigureAwait(false);
            if (verifyResult.ExitCode != 0)
            {
                throw new InvalidOperationException($"Base commit '{explicitBase}' could not be resolved: {verifyResult.StandardError.Trim()}");
            }
            return verifyResult.StandardOutput.Trim();
        }

        // Check CI environment
        var githubBaseRef = Environment.GetEnvironmentVariable("GITHUB_BASE_REF");
        if (!string.IsNullOrWhiteSpace(githubBaseRef))
        {
            var mergeBase = await GetMergeBaseAsync(workingDirectory, $"origin/{githubBaseRef}", cancellationToken).ConfigureAwait(false)
                            ?? await GetMergeBaseAsync(workingDirectory, githubBaseRef, cancellationToken).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(mergeBase))
            {
                return mergeBase;
            }
        }

        // Try merge-base with origin/main or main
        var mainMergeBase = await GetMergeBaseAsync(workingDirectory, "origin/main", cancellationToken).ConfigureAwait(false)
                            ?? await GetMergeBaseAsync(workingDirectory, "main", cancellationToken).ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(mainMergeBase))
        {
            return mainMergeBase;
        }

        // Local fallback: HEAD~1
        var headPrevRequest = new ProcessRequest("git", ["rev-parse", "HEAD~1"], workingDirectory, GitTimeout);
        var headPrevResult = await _processRunner.RunAsync(headPrevRequest, cancellationToken).ConfigureAwait(false);
        if (headPrevResult.ExitCode == 0)
        {
            return headPrevResult.StandardOutput.Trim();
        }

        // If HEAD~1 fails (e.g. single commit repo), return HEAD
        return await GetHeadCommitAsync(workingDirectory, cancellationToken).ConfigureAwait(false);
    }

    public async Task<ChangeSet> GetChangeSetAsync(string workingDirectory, string? baseCommit = null, CancellationToken cancellationToken = default)
    {
        var headCommit = await GetHeadCommitAsync(workingDirectory, cancellationToken).ConfigureAwait(false);
        var resolvedBase = baseCommit ?? await ResolveBaseCommitAsync(workingDirectory, null, cancellationToken).ConfigureAwait(false);

        var diffRequest = new ProcessRequest("git", ["diff", "--name-status", "-M", resolvedBase, headCommit], workingDirectory, GitTimeout);
        var diffResult = await _processRunner.RunAsync(diffRequest, cancellationToken).ConfigureAwait(false);
        if (diffResult.ExitCode != 0)
        {
            throw new InvalidOperationException($"Failed to compute git diff between '{resolvedBase}' and '{headCommit}': {diffResult.StandardError.Trim()}");
        }

        var files = ParseDiffNameStatus(diffResult.StandardOutput);
        return new ChangeSet(resolvedBase, headCommit, files);
    }

    public static IReadOnlyList<ChangedFile> ParseDiffNameStatus(string diffOutput)
    {
        var files = new List<ChangedFile>();
        if (string.IsNullOrWhiteSpace(diffOutput))
        {
            return files;
        }

        var lines = diffOutput.Split(["\r\n", "\r", "\n"], StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines)
        {
            var parts = line.Split('\t', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2) continue;

            var status = parts[0].Trim();
            if (status.StartsWith('A'))
            {
                files.Add(new ChangedFile(parts[1].Trim(), ChangeType.Added));
            }
            else if (status.StartsWith('M'))
            {
                files.Add(new ChangedFile(parts[1].Trim(), ChangeType.Modified));
            }
            else if (status.StartsWith('D'))
            {
                files.Add(new ChangedFile(parts[1].Trim(), ChangeType.Deleted));
            }
            else if (status.StartsWith('R'))
            {
                if (parts.Length >= 3)
                {
                    files.Add(new ChangedFile(parts[2].Trim(), ChangeType.Renamed, parts[1].Trim()));
                }
                else
                {
                    files.Add(new ChangedFile(parts[1].Trim(), ChangeType.Renamed));
                }
            }
            else
            {
                // Default to modified for unknown change types
                files.Add(new ChangedFile(parts[1].Trim(), ChangeType.Modified));
            }
        }

        return files;
    }
}
