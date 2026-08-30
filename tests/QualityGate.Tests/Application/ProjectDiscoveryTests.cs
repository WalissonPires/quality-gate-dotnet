using FluentAssertions;
using QualityGate.Application;
using QualityGate.Domain;
using Xunit;

namespace QualityGate.Tests.Application;

public sealed class ProjectDiscoveryTests
{
    private readonly ProjectDiscovery _discovery = new(Environment.CurrentDirectory);

    [Fact]
    public void FindOwningProject_ShouldMatchClosestDirectory()
    {
        var projects = new[]
        {
            "backend/Wamage.csproj",
            "backend.tests/Wamage.Tests.csproj",
            "frontend/app.csproj"
        };

        var owning = _discovery.FindOwningProject("backend/Features/Persons/Person.cs", projects);

        owning.Should().Be("backend/Wamage.csproj");
    }

    [Fact]
    public void FindAffectedProjects_WithChangeSet_ShouldReturnMatchedProjects()
    {
        var projects = new[]
        {
            "backend/Wamage.csproj",
            "backend.tests/Wamage.Tests.csproj"
        };

        var changeSet = new ChangeSet("base", "head", [
            new ChangedFile("backend/Features/Persons/Person.cs", ChangeType.Modified),
            new ChangedFile("docs/readme.md", ChangeType.Modified)
        ]);

        var affected = _discovery.FindAffectedProjects(changeSet, projects);

        affected.Should().ContainSingle().Which.Should().Be("backend/Wamage.csproj");
    }
}
