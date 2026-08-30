using FluentAssertions;
using QualityGate.Infrastructure.Process;
using Xunit;

namespace QualityGate.Tests.Infrastructure;

public sealed class ProcessRunnerTests
{
    private readonly ProcessRunner _runner = new();

    [Fact]
    public async Task RunAsync_WithValidDotnetCommand_ShouldSucceed()
    {
        var request = new ProcessRequest(
            "dotnet",
            ["--version"],
            Environment.CurrentDirectory,
            TimeSpan.FromSeconds(10));

        var result = await _runner.RunAsync(request);

        result.ExitCode.Should().Be(0);
        result.StandardOutput.Should().NotBeNullOrWhiteSpace();
        result.TimedOut.Should().BeFalse();
    }

    [Fact]
    public async Task RunAsync_WhenTimesOut_ShouldReturnTimedOut()
    {
        var isWindows = OperatingSystem.IsWindows();
        var fileName = isWindows ? "powershell.exe" : "sleep";
        var args = isWindows ? new[] { "-Command", "Start-Sleep -Seconds 10" } : new[] { "10" };

        var request = new ProcessRequest(
            fileName,
            args,
            Environment.CurrentDirectory,
            TimeSpan.FromMilliseconds(200));

        var result = await _runner.RunAsync(request);

        result.TimedOut.Should().BeTrue();
        result.ExitCode.Should().Be(-1);
    }
    [Fact]
    public async Task RunAsync_WhenCancelled_ShouldThrowOperationCanceledException()
    {
        var isWindows = OperatingSystem.IsWindows();
        var fileName = isWindows ? "powershell.exe" : "sleep";
        var args = isWindows ? new[] { "-Command", "Start-Sleep -Seconds 10" } : new[] { "10" };

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
        var request = new ProcessRequest(
            fileName,
            args,
            Environment.CurrentDirectory,
            TimeSpan.FromSeconds(30));

        var act = async () => await _runner.RunAsync(request, cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}
