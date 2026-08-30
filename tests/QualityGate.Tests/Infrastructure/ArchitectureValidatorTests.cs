using FluentAssertions;
using QualityGate.Configuration;
using QualityGate.Infrastructure.Architecture;
using Xunit;

namespace QualityGate.Tests.Infrastructure;

public sealed class ArchitectureValidatorTests
{
    private readonly ArchitectureValidator _validator = new();

    [Theory]
    [InlineData("Wamage.Features.Persons.Domain", "Wamage.Features.*.Domain", true)]
    [InlineData("Wamage.Features.Persons.Application", "Wamage.Features.*.Domain", false)]
    [InlineData("Microsoft.EntityFrameworkCore", "Microsoft.EntityFrameworkCore", true)]
    [InlineData("Microsoft.EntityFrameworkCore.SqlServer", "Microsoft.EntityFrameworkCore", true)]
    [InlineData("System.Text.Json", "Microsoft.EntityFrameworkCore", false)]
    public void MatchesPattern_ShouldMatchCorrectly(string actual, string pattern, bool expected)
    {
        var result = ArchitectureValidator.MatchesPattern(actual, pattern);
        result.Should().Be(expected);
    }

    [Fact]
    public async Task ValidateAsync_WhenForbiddenUsingPresent_DetectsViolation()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.cs");
        var code = """
        namespace Wamage.Features.Persons.Domain;

        using Microsoft.EntityFrameworkCore;
        using System;

        public class Person
        {
        }
        """;

        await File.WriteAllTextAsync(tempFile, code);

        var rules = new List<ArchitectureRuleOptions>
        {
            new()
            {
                Name = "DomainIsolation",
                Source = "Wamage.Features.*.Domain",
                ForbiddenDependencies = ["Microsoft.EntityFrameworkCore"]
            }
        };

        try
        {
            var violations = await _validator.ValidateAsync([tempFile], rules);

            violations.Should().ContainSingle();
            violations[0].RuleName.Should().Be("DomainIsolation");
            violations[0].ForbiddenDependency.Should().Be("Microsoft.EntityFrameworkCore");
            violations[0].LineNumber.Should().Be(3);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }
}
