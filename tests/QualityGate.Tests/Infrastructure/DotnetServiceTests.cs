using FluentAssertions;
using NSubstitute;
using QualityGate.Infrastructure.Dotnet;
using QualityGate.Infrastructure.Process;
using Xunit;

namespace QualityGate.Tests.Infrastructure;

public sealed class DotnetServiceTests
{
    private readonly IProcessRunner _processRunner = Substitute.For<IProcessRunner>();
    private readonly DotnetService _sut;

    public DotnetServiceTests()
    {
        _sut = new DotnetService(_processRunner);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private void SetupProcessResult(string stdout = "", string stderr = "", int exitCode = 0)
    {
        _processRunner
            .RunAsync(Arg.Any<ProcessRequest>(), Arg.Any<CancellationToken>())
            .Returns(new ProcessResult(exitCode, stdout, stderr, TimeSpan.FromMilliseconds(100)));
    }

    // ── --collect argument format ─────────────────────────────────────────────

    [Fact]
    public async Task TestAsync_WithCoverage_ShouldPassExplicitCollectArgument()
    {
        SetupProcessResult("Passed: 1, Total: 1");

        await _sut.TestAsync("some.csproj", collectCoverage: true);

        await _processRunner.Received(1).RunAsync(
            Arg.Is<ProcessRequest>(r =>
                r.Arguments.Contains("--collect") &&
                r.Arguments.SkipWhile(a => a != "--collect").Skip(1).FirstOrDefault() == "XPlat Code Coverage;Format=cobertura"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TestAsync_WithCustomCoverageFormat_ShouldIncludeFormatInArg()
    {
        SetupProcessResult("Passed: 1, Total: 1");

        await _sut.TestAsync("some.csproj", collectCoverage: true, coverageFormat: "json");

        await _processRunner.Received(1).RunAsync(
            Arg.Is<ProcessRequest>(r =>
                r.Arguments.Contains("--collect") &&
                r.Arguments.SkipWhile(a => a != "--collect").Skip(1).FirstOrDefault() == "XPlat Code Coverage;Format=json"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TestAsync_WithoutCoverage_ShouldNotPassCollectArg()
    {
        SetupProcessResult("Passed: 1, Total: 1");

        await _sut.TestAsync("some.csproj", collectCoverage: false);

        await _processRunner.Received(1).RunAsync(
            Arg.Is<ProcessRequest>(r => !r.Arguments.Contains("--collect")),
            Arg.Any<CancellationToken>());
    }

    // ── --filter argument ─────────────────────────────────────────────────────

    [Fact]
    public async Task TestAsync_WithTestFilter_ShouldPassFilterArg()
    {
        SetupProcessResult("Passed: 1, Total: 1");

        await _sut.TestAsync("some.csproj", testFilter: "FullyQualifiedName~PersonTests");

        await _processRunner.Received(1).RunAsync(
            Arg.Is<ProcessRequest>(r =>
                r.Arguments.Contains("--filter") &&
                r.Arguments.SkipWhile(a => a != "--filter").Skip(1).FirstOrDefault() == "FullyQualifiedName~PersonTests"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TestAsync_WithoutTestFilter_ShouldNotPassFilterArg()
    {
        SetupProcessResult("Passed: 1, Total: 1");

        await _sut.TestAsync("some.csproj");

        await _processRunner.Received(1).RunAsync(
            Arg.Is<ProcessRequest>(r => !r.Arguments.Contains("--filter")),
            Arg.Any<CancellationToken>());
    }

    // ── test summary parsing ──────────────────────────────────────────────────

    [Fact]
    public async Task TestAsync_ShouldParseTestSummaryFromStdout()
    {
        SetupProcessResult("Passed: 38, Failed: 2, Skipped: 1, Total: 41");

        var result = await _sut.TestAsync("some.csproj", collectCoverage: false);

        result.TotalTests.Should().Be(41);
        result.PassedTests.Should().Be(38);
        result.FailedTests.Should().Be(2);
        result.SkippedTests.Should().Be(1);
    }

    [Fact]
    public async Task TestAsync_WhenNoCoverageFileFound_ShouldReturnNullCoverageReportPath()
    {
        SetupProcessResult("Passed: 1, Total: 1");
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

        var result = await _sut.TestAsync("some.csproj", resultsDirectory: tempDir, collectCoverage: true);

        result.CoverageReportPath.Should().BeNull();
    }

    // ── ParseTestSummary (static) ─────────────────────────────────────────────

    [Theory]
    [InlineData("Total tests: 10. Passed: 8. Failed: 1. Skipped: 1.", 10, 8, 1, 1)]
    [InlineData("Passed: 22, Total: 22", 22, 22, 0, 0)]
    [InlineData("Com falha: 3, Aprovado: 7, Ignorado: 0, Total: 10", 10, 7, 3, 0)]
    [InlineData("", 0, 0, 0, 0)]
    public void ParseTestSummary_ShouldParseVariousFormats(
        string output, int expectedTotal, int expectedPassed, int expectedFailed, int expectedSkipped)
    {
        var (total, passed, failed, skipped) = DotnetService.ParseTestSummary(output);

        total.Should().Be(expectedTotal);
        passed.Should().Be(expectedPassed);
        failed.Should().Be(expectedFailed);
        skipped.Should().Be(expectedSkipped);
    }
}
