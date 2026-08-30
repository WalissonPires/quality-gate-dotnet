using FluentAssertions;
using QualityGate.Domain;
using Xunit;

namespace QualityGate.Tests.Domain;

public sealed class GateResultTests
{
    [Fact]
    public void Pass_ShouldHavePassedTrueAndPassedStatus()
    {
        var result = GateResult.Pass("Build", "Build succeeded.", TimeSpan.FromSeconds(2));

        result.Gate.Should().Be("Build");
        result.Status.Should().Be(GateStatus.Passed);
        result.Passed.Should().BeTrue();
        result.Message.Should().Be("Build succeeded.");
    }

    [Fact]
    public void Fail_ShouldHavePassedFalseAndFailedStatus()
    {
        var result = GateResult.Fail("Test", "3 tests failed.", TimeSpan.FromSeconds(5));

        result.Gate.Should().Be("Test");
        result.Status.Should().Be(GateStatus.Failed);
        result.Passed.Should().BeFalse();
    }

    [Fact]
    public void Error_ShouldHavePassedFalseAndErrorStatus()
    {
        var result = GateResult.Error("Coverage", "Tool crashed.", TimeSpan.FromSeconds(1));

        result.Gate.Should().Be("Coverage");
        result.Status.Should().Be(GateStatus.Error);
        result.Passed.Should().BeFalse();
    }

    [Fact]
    public void Skip_ShouldHavePassedTrueAndSkippedStatus()
    {
        var result = GateResult.Skip("Mutation", "Skipped.");

        result.Gate.Should().Be("Mutation");
        result.Status.Should().Be(GateStatus.Skipped);
        result.Passed.Should().BeTrue();
    }

    [Fact]
    public void NotApplicable_ShouldHavePassedTrueAndNotApplicableStatus()
    {
        var result = GateResult.NotApplicable("Coverage", "No executable lines changed.");

        result.Gate.Should().Be("Coverage");
        result.Status.Should().Be(GateStatus.NotApplicable);
        result.Passed.Should().BeTrue();
    }
}
