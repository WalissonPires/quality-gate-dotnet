using FluentAssertions;
using NSubstitute;
using QualityGate.Commands;
using QualityGate.Configuration;
using QualityGate.Domain;
using QualityGate.Infrastructure.Architecture;
using QualityGate.Infrastructure.Coverage;
using QualityGate.Infrastructure.Dotnet;
using QualityGate.Infrastructure.Git;
using QualityGate.Infrastructure.Process;
using QualityGate.Infrastructure.Roslyn;
using Xunit;

namespace QualityGate.Tests.Commands;

public sealed class BaselineCommandTests
{
    private readonly IProcessRunner _processRunner = Substitute.For<IProcessRunner>();
    private readonly IGitService _gitService = Substitute.For<IGitService>();
    private readonly IDotnetService _dotnetService = Substitute.For<IDotnetService>();
    private readonly ICoverageParser _coverageParser = Substitute.For<ICoverageParser>();
    private readonly IArchitectureValidator _architectureValidator = Substitute.For<IArchitectureValidator>();
    private readonly IComplexityAnalyzer _complexityAnalyzer = Substitute.For<IComplexityAnalyzer>();

    public BaselineCommandTests()
    {
        _gitService.IsGitRepositoryAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);
        _gitService.GetHeadCommitAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns("commit-abc1234");

        _dotnetService.BuildAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<TimeSpan?>(), Arg.Any<CancellationToken>())
            .Returns(new DotnetBuildResult(0, "Build succeeded. 0 Warning(s)", string.Empty, TimeSpan.FromSeconds(1), false));

        _dotnetService.TestAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<bool>(), Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<TimeSpan?>(), Arg.Any<CancellationToken>())
            .Returns(new DotnetTestResult(0, 50, 50, 0, 0, "Passed: 50", string.Empty, TimeSpan.FromSeconds(2), false));

        _coverageParser.ParseMultipleAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>())
            .Returns(new CoverageSummary(100, 85, 20, 15, new Dictionary<string, FileCoverageReport>
            {
                ["backend/Features/Persons/Person.cs"] = new("backend/Features/Persons/Person.cs", 100, 85, 20, 15, new Dictionary<int, LineCoverageInfo>())
            }));

        _architectureValidator.ValidateAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<IEnumerable<ArchitectureRuleOptions>>(), Arg.Any<CancellationToken>())
            .Returns([]);

        _complexityAnalyzer.AnalyzeFilesAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>())
            .Returns(new ComplexityAnalysisResult([], []));
    }

    [Fact]
    public async Task RecordAsync_CreatesValidBaselineFile()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"baseline-record-{Guid.NewGuid():N}.json");

        try
        {
            var exitCode = await BaselineCommand.RecordAsync(
                outputPath: tempFile,
                configPath: "qualitygate.json",
                verbose: false,
                processRunner: _processRunner,
                gitService: _gitService,
                dotnetService: _dotnetService,
                coverageParser: _coverageParser,
                architectureValidator: _architectureValidator,
                complexityAnalyzer: _complexityAnalyzer);

            exitCode.Should().Be(0);
            File.Exists(tempFile).Should().BeTrue();

            var loaded = await BaselineLoader.LoadAsync(tempFile, Environment.CurrentDirectory);
            loaded.Should().NotBeNull();
            loaded.Metrics.TotalTests.Should().BeGreaterThanOrEqualTo(50);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task RecordAsync_WhenCoverageIsZeroWithPassingTests_ShouldAbortWithExitCode3()
    {
        // Coverage reports lines exist but zero are covered — untrustworthy artifact
        _dotnetService.TestAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<bool>(), Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<TimeSpan?>(), Arg.Any<CancellationToken>())
            .Returns(new DotnetTestResult(0, 50, 50, 0, 0, "Passed: 50", string.Empty, TimeSpan.FromSeconds(2), false));

        _coverageParser.ParseMultipleAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>())
            .Returns(new CoverageSummary(TotalLines: 5000, CoveredLines: 0, TotalBranches: 0, CoveredBranches: 0, FileReports: new Dictionary<string, FileCoverageReport>()));

        var tempFile = Path.Combine(Path.GetTempPath(), $"baseline-sanity-{Guid.NewGuid():N}.json");
        try
        {
            var exitCode = await BaselineCommand.RecordAsync(
                outputPath: tempFile,
                configPath: "qualitygate.json",
                verbose: false,
                processRunner: _processRunner,
                gitService: _gitService,
                dotnetService: _dotnetService,
                coverageParser: _coverageParser,
                architectureValidator: _architectureValidator,
                complexityAnalyzer: _complexityAnalyzer);

            exitCode.Should().Be(3);
            File.Exists(tempFile).Should().BeFalse("baseline must not be written when coverage is untrustworthy");
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task RecordAsync_WhenCoverageIsZeroAndTestsFailed_ShouldNotAbort()
    {
        // When tests failed, zero coverage is expected — sanity guard must NOT fire
        _dotnetService.TestAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<bool>(), Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<TimeSpan?>(), Arg.Any<CancellationToken>())
            .Returns(new DotnetTestResult(1, 50, 40, 10, 0, "Failed: 10", string.Empty, TimeSpan.FromSeconds(2), false));

        _coverageParser.ParseMultipleAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>())
            .Returns(new CoverageSummary(TotalLines: 5000, CoveredLines: 0, TotalBranches: 0, CoveredBranches: 0, FileReports: new Dictionary<string, FileCoverageReport>()));

        var tempFile = Path.Combine(Path.GetTempPath(), $"baseline-sanity-fail-{Guid.NewGuid():N}.json");
        try
        {
            var exitCode = await BaselineCommand.RecordAsync(
                outputPath: tempFile,
                configPath: "qualitygate.json",
                verbose: false,
                processRunner: _processRunner,
                gitService: _gitService,
                dotnetService: _dotnetService,
                coverageParser: _coverageParser,
                architectureValidator: _architectureValidator,
                complexityAnalyzer: _complexityAnalyzer);

            // Should succeed (exit 0) even though coverage is zero, because tests failed
            exitCode.Should().Be(0);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task RecordAsync_VerboseMode_ShouldSucceedAndPrintDiagnostics()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"baseline-verbose-{Guid.NewGuid():N}.json");
        var originalOut = Console.Out;
        using var sw = new System.IO.StringWriter();
        Console.SetOut(sw);

        try
        {
            var exitCode = await BaselineCommand.RecordAsync(
                outputPath: tempFile,
                configPath: "qualitygate.json",
                verbose: true,
                processRunner: _processRunner,
                gitService: _gitService,
                dotnetService: _dotnetService,
                coverageParser: _coverageParser,
                architectureValidator: _architectureValidator,
                complexityAnalyzer: _complexityAnalyzer);

            var output = sw.ToString();
            exitCode.Should().Be(0);
            output.Should().Contain("[Verbose]");
            output.Should().Contain("Coverage summary:");
        }
        finally
        {
            Console.SetOut(originalOut);
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }
}
