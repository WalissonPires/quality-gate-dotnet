using FluentAssertions;
using NSubstitute;
using QualityGate.Commands;
using QualityGate.Configuration;
using QualityGate.Domain;
using QualityGate.Gates;
using QualityGate.Infrastructure.Coverage;
using QualityGate.Infrastructure.Dotnet;
using QualityGate.Infrastructure.Git;
using QualityGate.Infrastructure.Process;
using Xunit;

namespace QualityGate.Tests.Commands;

public sealed class CheckCommandTests
{
    private readonly IProcessRunner _processRunner = Substitute.For<IProcessRunner>();
    private readonly IGitService _gitService = Substitute.For<IGitService>();
    private readonly IDotnetService _dotnetService = Substitute.For<IDotnetService>();
    private readonly ICoverageParser _coverageParser = Substitute.For<ICoverageParser>();

    public CheckCommandTests()
    {
        _processRunner.RunAsync(Arg.Is<ProcessRequest>(r => r.FileName == "dotnet" && r.Arguments.Contains("--version")), Arg.Any<CancellationToken>())
            .Returns(new ProcessResult(0, "10.0.302\n", string.Empty, TimeSpan.FromMilliseconds(20)));

        _gitService.IsGitRepositoryAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);
        _gitService.GetHeadCommitAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns("head123");
        _gitService.GetChangeSetAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(new ChangeSet("base", "head", [new ChangedFile("backend/App.cs", ChangeType.Modified)]));
    }

    [Fact]
    public async Task ExecuteAsync_WhenAllGatesPass_ReturnsExitCode0()
    {
        var gate = Substitute.For<IQualityGate>();
        gate.Name.Returns("Build");
        gate.ExecuteAsync(Arg.Any<QualityContext>(), Arg.Any<CancellationToken>())
            .Returns(GateResult.Pass("Build", "Succeeded", TimeSpan.FromSeconds(1)));

        var exitCode = await CheckCommand.ExecuteAsync(
            diff: true, ns: null, project: null, repository: false, baseRef: null,
            format: "console", skip: [], only: [], failFast: false, verbose: false,
            configPath: "qualitygate.json",
            processRunner: _processRunner,
            gitService: _gitService,
            dotnetService: _dotnetService,
            coverageParser: _coverageParser,
            customGates: [gate]);

        exitCode.Should().Be(0);
    }

    [Fact]
    public async Task ExecuteAsync_WhenGateFails_ReturnsExitCode1()
    {
        var gate = Substitute.For<IQualityGate>();
        gate.Name.Returns("Build");
        gate.ExecuteAsync(Arg.Any<QualityContext>(), Arg.Any<CancellationToken>())
            .Returns(GateResult.Fail("Build", "Build failed", TimeSpan.FromSeconds(1)));

        var exitCode = await CheckCommand.ExecuteAsync(
            diff: true, ns: null, project: null, repository: false, baseRef: null,
            format: "console", skip: [], only: [], failFast: false, verbose: false,
            configPath: "qualitygate.json",
            processRunner: _processRunner,
            gitService: _gitService,
            dotnetService: _dotnetService,
            coverageParser: _coverageParser,
            customGates: [gate]);

        exitCode.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteAsync_WhenMutuallyExclusiveOptionsGiven_ReturnsExitCode2()
    {
        var exitCode = await CheckCommand.ExecuteAsync(
            diff: true, ns: null, project: null, repository: false, baseRef: null,
            format: "console", skip: ["build"], only: ["test"], failFast: false, verbose: false,
            configPath: "qualitygate.json");

        exitCode.Should().Be(2);
    }

    [Fact]
    public async Task ExecuteAsync_WhenGateEncounterError_ReturnsExitCode3()
    {
        var gate = Substitute.For<IQualityGate>();
        gate.Name.Returns("Build");
        gate.ExecuteAsync(Arg.Any<QualityContext>(), Arg.Any<CancellationToken>())
            .Returns(GateResult.Error("Build", "External tool crashed", TimeSpan.FromSeconds(1)));

        var exitCode = await CheckCommand.ExecuteAsync(
            diff: true, ns: null, project: null, repository: false, baseRef: null,
            format: "console", skip: [], only: [], failFast: false, verbose: false,
            configPath: "qualitygate.json",
            processRunner: _processRunner,
            gitService: _gitService,
            dotnetService: _dotnetService,
            coverageParser: _coverageParser,
            customGates: [gate]);

        exitCode.Should().Be(3);
    }

    [Fact]
    public async Task ExecuteAsync_WhenScopeCannotBeResolved_ReturnsExitCode4()
    {
        _gitService.IsGitRepositoryAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(false);

        var exitCode = await CheckCommand.ExecuteAsync(
            diff: true, ns: null, project: null, repository: false, baseRef: null,
            format: "console", skip: [], only: [], failFast: false, verbose: false,
            configPath: "qualitygate.json",
            processRunner: _processRunner,
            gitService: _gitService,
            dotnetService: _dotnetService,
            coverageParser: _coverageParser);

        exitCode.Should().Be(4);
    }

    [Fact]
    public async Task ExecuteAsync_WhenFormatIsInvalid_ReturnsExitCode2()
    {
        var exitCode = await CheckCommand.ExecuteAsync(
            diff: true, ns: null, project: null, repository: false, baseRef: null,
            format: "unsupported_format", skip: [], only: [], failFast: false, verbose: false,
            configPath: "qualitygate.json");

        exitCode.Should().Be(2);
    }

    [Theory]
    [InlineData("md")]
    [InlineData("markdown")]
    public async Task ExecuteAsync_WhenFormatIsMarkdown_PrintsMarkdown(string format)
    {
        var gate = Substitute.For<IQualityGate>();
        gate.Name.Returns("Build");
        gate.ExecuteAsync(Arg.Any<QualityContext>(), Arg.Any<CancellationToken>())
            .Returns(GateResult.Pass("Build", "Succeeded", TimeSpan.FromSeconds(1)));

        using var sw = new StringWriter();
        var originalOut = Console.Out;
        Console.SetOut(sw);

        try
        {
            var exitCode = await CheckCommand.ExecuteAsync(
                diff: true, ns: null, project: null, repository: false, baseRef: null,
                format: format, skip: [], only: [], failFast: false, verbose: false,
                configPath: "qualitygate.json",
                processRunner: _processRunner,
                gitService: _gitService,
                dotnetService: _dotnetService,
                coverageParser: _coverageParser,
                customGates: [gate]);

            exitCode.Should().Be(0);
            var output = sw.ToString();
            output.Should().Contain("# Wamage Quality Gate Report");
            output.Should().Contain("✅ **PASSED**");
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    [Fact]
    public async Task ExecuteAsync_WhenFormatIsBoth_GeneratesBothJsonAndMdReports()
    {
        var gate = Substitute.For<IQualityGate>();
        gate.Name.Returns("Build");
        gate.ExecuteAsync(Arg.Any<QualityContext>(), Arg.Any<CancellationToken>())
            .Returns(GateResult.Pass("Build", "Succeeded", TimeSpan.FromSeconds(1)));

        var artifactsBase = Path.Combine(Environment.CurrentDirectory, ".qualitygate", "artifacts");
        var beforeDirs = Directory.Exists(artifactsBase)
            ? Directory.GetDirectories(artifactsBase).ToHashSet()
            : [];

        var exitCode = await CheckCommand.ExecuteAsync(
            diff: true, ns: null, project: null, repository: false, baseRef: null,
            format: "both", skip: [], only: [], failFast: false, verbose: false,
            configPath: "qualitygate.json",
            processRunner: _processRunner,
            gitService: _gitService,
            dotnetService: _dotnetService,
            coverageParser: _coverageParser,
            customGates: [gate]);

        exitCode.Should().Be(0);

        var afterDirs = Directory.GetDirectories(artifactsBase).ToHashSet();
        afterDirs.ExceptWith(beforeDirs);
        afterDirs.Should().ContainSingle();

        var newRunDir = afterDirs.Single();
        File.Exists(Path.Combine(newRunDir, "report.json")).Should().BeTrue();
        File.Exists(Path.Combine(newRunDir, "report.md")).Should().BeTrue();

        // Clean up test artifact folder
        try
        {
            Directory.Delete(newRunDir, recursive: true);
        }
        catch
        {
            // Ignore cleanup errors
        }
    }
}
