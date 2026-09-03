using FluentAssertions;
using NSubstitute;
using QualityGate.Domain;
using QualityGate.Infrastructure.Git;
using QualityGate.Infrastructure.Process;
using Xunit;

namespace QualityGate.Tests.Infrastructure;

public sealed class GitServiceTests
{
    private readonly IProcessRunner _processRunner = Substitute.For<IProcessRunner>();
    private readonly GitService _gitService;

    public GitServiceTests()
    {
        _gitService = new GitService(_processRunner);
    }

    [Fact]
    public void ParseDiffNameStatus_ShouldParseAllChangeTypes()
    {
        var diffOutput = """
        A	backend/NewFile.cs
        M	backend/ExistingFile.cs
        D	backend/DeletedFile.cs
        R100	backend/OldName.cs	backend/NewName.cs
        """;

        var files = GitService.ParseDiffNameStatus(diffOutput);

        files.Should().HaveCount(4);

        var added = files[0];
        added.Path.Should().Be("backend/NewFile.cs");
        added.ChangeType.Should().Be(ChangeType.Added);

        var modified = files[1];
        modified.Path.Should().Be("backend/ExistingFile.cs");
        modified.ChangeType.Should().Be(ChangeType.Modified);

        var deleted = files[2];
        deleted.Path.Should().Be("backend/DeletedFile.cs");
        deleted.ChangeType.Should().Be(ChangeType.Deleted);

        var renamed = files[3];
        renamed.Path.Should().Be("backend/NewName.cs");
        renamed.OldPath.Should().Be("backend/OldName.cs");
        renamed.ChangeType.Should().Be(ChangeType.Renamed);
    }

    [Fact]
    public async Task IsGitRepositoryAsync_WhenProcessReturnsTrue_ShouldReturnTrue()
    {
        _processRunner.RunAsync(Arg.Is<ProcessRequest>(r => r.FileName == "git" && r.Arguments.Contains("--is-inside-work-tree")), Arg.Any<CancellationToken>())
            .Returns(new ProcessResult(0, "true\n", string.Empty, TimeSpan.FromMilliseconds(50)));

        var isGit = await _gitService.IsGitRepositoryAsync(Environment.CurrentDirectory);

        isGit.Should().BeTrue();
    }

    [Fact]
    public async Task GetHeadCommitAsync_ShouldReturnTrimmedSha()
    {
        _processRunner.RunAsync(Arg.Is<ProcessRequest>(r => r.FileName == "git" && r.Arguments.Contains("HEAD")), Arg.Any<CancellationToken>())
            .Returns(new ProcessResult(0, "a1b2c3d4e5f6\n", string.Empty, TimeSpan.FromMilliseconds(50)));

        var head = await _gitService.GetHeadCommitAsync(Environment.CurrentDirectory);

        head.Should().Be("a1b2c3d4e5f6");
    }

    [Fact]
    public async Task HasUncommittedChangesAsync_WhenStatusNotEmpty_ReturnsTrue()
    {
        _processRunner.RunAsync(Arg.Is<ProcessRequest>(r => r.FileName == "git" && r.Arguments.Contains("--porcelain")), Arg.Any<CancellationToken>())
            .Returns(new ProcessResult(0, " M src/App.cs\n?? newfile.cs\n", string.Empty, TimeSpan.FromMilliseconds(50)));

        var hasChanges = await _gitService.HasUncommittedChangesAsync(Environment.CurrentDirectory);

        hasChanges.Should().BeTrue();
    }

    [Fact]
    public async Task HasUncommittedChangesAsync_WhenStatusEmpty_ReturnsFalse()
    {
        _processRunner.RunAsync(Arg.Is<ProcessRequest>(r => r.FileName == "git" && r.Arguments.Contains("--porcelain")), Arg.Any<CancellationToken>())
            .Returns(new ProcessResult(0, "", string.Empty, TimeSpan.FromMilliseconds(50)));

        var hasChanges = await _gitService.HasUncommittedChangesAsync(Environment.CurrentDirectory);

        hasChanges.Should().BeFalse();
    }

    [Fact]
    public async Task GetChangeSetAsync_WhenIncludeWorkingTree_IncludesDiffAndUntrackedFiles()
    {
        // rev-parse for base
        _processRunner.RunAsync(Arg.Is<ProcessRequest>(r => r.FileName == "git" && r.Arguments.Contains("rev-parse") && r.Arguments.Contains("origin/main")), Arg.Any<CancellationToken>())
            .Returns(new ProcessResult(0, "base_sha\n", string.Empty, TimeSpan.FromMilliseconds(50)));

        // diff base (against working tree)
        _processRunner.RunAsync(Arg.Is<ProcessRequest>(r => r.FileName == "git" && r.Arguments.Contains("diff") && !r.Arguments.Contains("HEAD")), Arg.Any<CancellationToken>())
            .Returns(new ProcessResult(0, "M\tsrc/App.cs\n", string.Empty, TimeSpan.FromMilliseconds(50)));

        // untracked files
        _processRunner.RunAsync(Arg.Is<ProcessRequest>(r => r.FileName == "git" && r.Arguments.Contains("ls-files")), Arg.Any<CancellationToken>())
            .Returns(new ProcessResult(0, "src/NewUntracked.cs\n", string.Empty, TimeSpan.FromMilliseconds(50)));

        var changeSet = await _gitService.GetChangeSetAsync(Environment.CurrentDirectory, baseCommit: "base_sha", includeWorkingTree: true);

        changeSet.BaseCommit.Should().Be("base_sha");
        changeSet.HeadCommit.Should().Be("WORKING_TREE");
        changeSet.Files.Should().HaveCount(2);
        changeSet.Files.Should().Contain(f => f.Path == "src/App.cs" && f.ChangeType == ChangeType.Modified);
        changeSet.Files.Should().Contain(f => f.Path == "src/NewUntracked.cs" && f.ChangeType == ChangeType.Added);
    }
}
