using FluentAssertions;
using NSubstitute;
using QualityGate.Application;
using QualityGate.Configuration;
using QualityGate.Domain;
using QualityGate.Infrastructure.Git;
using Xunit;

namespace QualityGate.Tests.Application;

public sealed class TargetResolverTests
{
    private readonly IGitService _gitService = Substitute.For<IGitService>();
    private readonly TargetResolver _resolver;

    public TargetResolverTests()
    {
        _resolver = new TargetResolver(_gitService, Environment.CurrentDirectory);
    }

    [Fact]
    public async Task ResolveAsync_WithDiffFlag_WhenInGit_ReturnsDiffTarget()
    {
        _gitService.IsGitRepositoryAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);

        var target = await _resolver.ResolveAsync(diffFlag: true, namespaceOption: null, projectOption: null, repositoryFlag: false);

        target.Scope.Should().Be(QualityScope.Diff);
    }

    [Fact]
    public async Task ResolveAsync_WithRepositoryFlag_ReturnsRepositoryTarget()
    {
        var target = await _resolver.ResolveAsync(diffFlag: false, namespaceOption: null, projectOption: null, repositoryFlag: true);

        target.Scope.Should().Be(QualityScope.Repository);
    }

    [Fact]
    public async Task ResolveAsync_WithNamespaceOption_ReturnsNamespaceTarget()
    {
        var target = await _resolver.ResolveAsync(diffFlag: false, namespaceOption: "Wamage.Features.Persons", projectOption: null, repositoryFlag: false);

        target.Scope.Should().Be(QualityScope.Namespace);
        target.Namespace.Should().Be("Wamage.Features.Persons");
    }

    [Fact]
    public async Task ResolveAsync_WithMutuallyExclusiveFlags_ThrowsConfigurationException()
    {
        var act = async () => await _resolver.ResolveAsync(diffFlag: true, namespaceOption: "Wamage", projectOption: null, repositoryFlag: false);

        await act.Should().ThrowAsync<ConfigurationException>()
            .WithMessage("*mutually exclusive*");
    }

    [Fact]
    public async Task ResolveAsync_WithAutoDetectInGit_ReturnsDiff()
    {
        _gitService.IsGitRepositoryAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);

        var target = await _resolver.ResolveAsync(diffFlag: false, namespaceOption: null, projectOption: null, repositoryFlag: false);

        target.Scope.Should().Be(QualityScope.Diff);
    }

    [Fact]
    public async Task ResolveAsync_WithAutoDetectNotInGit_ThrowsScopeException()
    {
        _gitService.IsGitRepositoryAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(false);

        var act = async () => await _resolver.ResolveAsync(diffFlag: false, namespaceOption: null, projectOption: null, repositoryFlag: false);

        await act.Should().ThrowAsync<ScopeException>();
    }

    [Fact]
    public void ShouldExpandToProjectScope_WhenStructuralFileChanged_ReturnsTrue()
    {
        var changeSet = new ChangeSet("base", "head", [
            new ChangedFile("Directory.Build.props", ChangeType.Modified)
        ]);

        TargetResolver.ShouldExpandToProjectScope(changeSet).Should().BeTrue();
    }
}
