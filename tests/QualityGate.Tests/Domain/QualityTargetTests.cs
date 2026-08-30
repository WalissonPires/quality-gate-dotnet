using FluentAssertions;
using QualityGate.Domain;
using Xunit;

namespace QualityGate.Tests.Domain;

public sealed class QualityTargetTests
{
    [Fact]
    public void ForDiff_ShouldCreateDiffScope()
    {
        var target = QualityTarget.ForDiff();

        target.Scope.Should().Be(QualityScope.Diff);
        target.Project.Should().BeNull();
        target.Namespace.Should().BeNull();
    }

    [Fact]
    public void ForRepository_ShouldCreateRepositoryScope()
    {
        var target = QualityTarget.ForRepository();

        target.Scope.Should().Be(QualityScope.Repository);
        target.Project.Should().BeNull();
        target.Namespace.Should().BeNull();
    }

    [Fact]
    public void ForProject_WithValidPath_ShouldCreateProjectScope()
    {
        var target = QualityTarget.ForProject("backend/Wamage.csproj");

        target.Scope.Should().Be(QualityScope.Project);
        target.Project.Should().Be("backend/Wamage.csproj");
        target.Namespace.Should().BeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ForProject_WithNullOrWhitespace_ShouldThrow(string? project)
    {
        var act = () => new QualityTarget(QualityScope.Project, project: project);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*Project path is required*");
    }

    [Fact]
    public void ForNamespace_WithValidNamespace_ShouldCreateNamespaceScope()
    {
        var target = QualityTarget.ForNamespace("Wamage.Features.Persons");

        target.Scope.Should().Be(QualityScope.Namespace);
        target.Namespace.Should().Be("Wamage.Features.Persons");
        target.Project.Should().BeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ForNamespace_WithNullOrWhitespace_ShouldThrow(string? @namespace)
    {
        var act = () => new QualityTarget(QualityScope.Namespace, @namespace: @namespace);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*Namespace is required*");
    }

    [Fact]
    public void DiffScope_WithProjectOrNamespace_ShouldThrow()
    {
        var act = () => new QualityTarget(QualityScope.Diff, project: "some.csproj");

        act.Should().Throw<ArgumentException>()
            .WithMessage("*must be null when scope is Diff*");
    }
}
