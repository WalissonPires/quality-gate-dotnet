using FluentAssertions;
using QualityGate.Domain;
using Xunit;

namespace QualityGate.Tests.Domain;

public sealed class GeneratedCodeFilterTests
{
    [Theory]
    [InlineData("Features/Users/Migrations/20260902120000_AddUser.Designer.cs", true)]
    [InlineData("Data/AppDbContextModelSnapshot.cs", true)]
    [InlineData("Arbitrary/Folder/Structure/MyContextModelSnapshot.cs", true)]
    [InlineData("Generated/MyService.g.cs", true)]
    [InlineData("Xaml/MainWindow.g.i.cs", true)]
    [InlineData("Features/Users/UserService.cs", false)]
    [InlineData("Controllers/ApiController.cs", false)]
    [InlineData("", false)]
    public void IsGeneratedOrIgnored_DefaultSuffixes_ShouldDetectCorrectly(string path, bool expected)
    {
        var result = GeneratedCodeFilter.IsGeneratedOrIgnored(path);
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("CustomMigrations/20260902_Init.cs", new[] { "**/CustomMigrations/**" }, true)]
    [InlineData("src/Database/Scripts/Migration.cs", new[] { "**/Database/Scripts/**" }, true)]
    [InlineData("src/Services/NormalService.cs", new[] { "**/CustomMigrations/**" }, false)]
    public void IsGeneratedOrIgnored_WithCustomIgnorePatterns_ShouldMatch(string path, string[] patterns, bool expected)
    {
        var result = GeneratedCodeFilter.IsGeneratedOrIgnored(path, patterns);
        result.Should().Be(expected);
    }

    [Fact]
    public void FilterFiles_ShouldRemoveGeneratedAndIgnoredFiles()
    {
        var files = new List<ChangedFile>
        {
            new("Services/AuthService.cs", ChangeType.Modified),
            new("Data/Migrations/20260902_AddRoles.Designer.cs", ChangeType.Added),
            new("Data/AppDbContextModelSnapshot.cs", ChangeType.Modified),
            new("Generated/Proto.g.cs", ChangeType.Added),
            new("Legacy/OldCode.cs", ChangeType.Modified)
        };

        var filtered = GeneratedCodeFilter.FilterFiles(files, ["**/Legacy/**"]);

        filtered.Should().HaveCount(1);
        filtered[0].Path.Should().Be("Services/AuthService.cs");
    }

    [Fact]
    public void FilterPaths_ShouldRemoveGeneratedAndIgnoredPaths()
    {
        var paths = new List<string>
        {
            "Domain/Order.cs",
            "Infrastructure/Persistence/2026_Migration.Designer.cs",
            "Infrastructure/Persistence/ContextModelSnapshot.cs",
            "Special/Code.cs"
        };

        var filtered = GeneratedCodeFilter.FilterPaths(paths, ["**/Special/**"]);

        filtered.Should().HaveCount(1);
        filtered[0].Should().Be("Domain/Order.cs");
    }
}
