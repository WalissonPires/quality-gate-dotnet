using System.Diagnostics;
using System.Text;

namespace QualityGate.Infrastructure.Process;

public sealed class ProcessRunner : IProcessRunner
{
    private const int MaxStreamBytes = 1024 * 1024; // 1 MB limit

    public async Task<ProcessResult> RunAsync(ProcessRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var stopwatch = Stopwatch.StartNew();
        var startInfo = new ProcessStartInfo
        {
            FileName = request.FileName,
            WorkingDirectory = request.WorkingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        foreach (var arg in request.Arguments)
        {
            startInfo.ArgumentList.Add(arg);
        }

        if (request.EnvironmentVariables != null)
        {
            foreach (var (key, value) in request.EnvironmentVariables)
            {
                startInfo.Environment[key] = value;
            }
        }

        using var process = new System.Diagnostics.Process { StartInfo = startInfo };
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        linkedCts.CancelAfter(request.Timeout);

        var stdoutBuilder = new StringBuilder();
        var stderrBuilder = new StringBuilder();
        var stdoutLock = new object();
        var stderrLock = new object();

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data == null) return;
            lock (stdoutLock)
            {
                if (stdoutBuilder.Length < MaxStreamBytes)
                {
                    stdoutBuilder.AppendLine(e.Data);
                }
            }
        };

        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data == null) return;
            lock (stderrLock)
            {
                if (stderrBuilder.Length < MaxStreamBytes)
                {
                    stderrBuilder.AppendLine(e.Data);
                }
            }
        };

        bool timedOut = false;
        try
        {
            if (!process.Start())
            {
                return new ProcessResult(-1, string.Empty, "Failed to start process.", stopwatch.Elapsed);
            }

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            await process.WaitForExitAsync(linkedCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            if (!cancellationToken.IsCancellationRequested)
            {
                timedOut = true;
            }

            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
            }
            catch
            {
                // Ignore kill errors if already terminated
            }

            if (!timedOut)
            {
                throw;
            }
        }
        finally
        {
            stopwatch.Stop();
        }

        string stdout;
        string stderr;
        lock (stdoutLock) { stdout = stdoutBuilder.ToString(); }
        lock (stderrLock) { stderr = stderrBuilder.ToString(); }

        int exitCode = timedOut ? -1 : (process.HasExited ? process.ExitCode : -1);
        return new ProcessResult(exitCode, stdout, stderr, stopwatch.Elapsed, timedOut);
    }
}
