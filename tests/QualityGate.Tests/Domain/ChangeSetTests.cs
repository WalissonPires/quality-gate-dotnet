using FluentAssertions;
using QualityGate.Domain;
using Xunit;

namespace QualityGate.Tests.Domain;

public sealed class ChangeSetTests
{
    [Fact]
    public void ChangeSet_WithValidFiles_ShouldExposeNonDeletedAndCSharpFiles()
    {
        var files = new List<ChangedFile>
        {
            new("backend/Person.cs", ChangeType.Added),
            new("backend/Service.cs", ChangeType.Modified),
            new("backend/Old.cs", ChangeType.Deleted),
            new("README.md", ChangeType.Modified)
        };

        var changeSet = new ChangeSet("base123", "head456", files);

        changeSet.BaseCommit.Should().Be("base123");
        changeSet.HeadCommit.Should().Be("head456");
        changeSet.Files.Should().HaveCount(4);
        changeSet.NonDeletedFiles.Should().HaveCount(3);
        changeSet.CSharpSourceFiles.Should().HaveCount(2);
        changeSet.CSharpSourceFiles.Select(f => f.Path).Should().Contain(["backend/Person.cs", "backend/Service.cs"]);
    }

    [Theory]
    [InlineData(null, "head")]
    [InlineData("", "head")]
    [InlineData("base", null)]
    [InlineData("base", "")]
    public void ChangeSet_WithInvalidCommits_ShouldThrow(string? baseCommit, string? headCommit)
    {
        var act = () => new ChangeSet(baseCommit!, headCommit!, []);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ChangedFile_NormalizesBackslashes()
    {
        var file = new ChangedFile(@"backend\Features\Persons\Person.cs", ChangeType.Modified);
        file.Path.Should().Be("backend/Features/Persons/Person.cs");
    }

    [Fact]
    public void ChangedFile_Renamed_RequiresOldPath()
    {
        var act = () => new ChangedFile("new.cs", ChangeType.Renamed, null);
        act.Should().Throw<ArgumentException>();
    }
}
