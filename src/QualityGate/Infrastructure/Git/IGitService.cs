using QualityGate.Domain;

namespace QualityGate.Infrastructure.Git;

public interface IGitService
{
    Task<bool> IsGitRepositoryAsync(string workingDirectory, CancellationToken cancellationToken = default);
    Task<string> GetHeadCommitAsync(string workingDirectory, CancellationToken cancellationToken = default);
    Task<string?> GetMergeBaseAsync(string workingDirectory, string targetRef = "origin/main", CancellationToken cancellationToken = default);
    Task<string> ResolveBaseCommitAsync(string workingDirectory, string? explicitBase = null, CancellationToken cancellationToken = default);
    Task<ChangeSet> GetChangeSetAsync(string workingDirectory, string? baseCommit = null, CancellationToken cancellationToken = default);
}
