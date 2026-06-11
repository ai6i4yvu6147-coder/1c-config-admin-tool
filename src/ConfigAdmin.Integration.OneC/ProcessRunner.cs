using System.Diagnostics;
using ConfigAdmin.Domain.Integration;

namespace ConfigAdmin.Integration.OneC;

public sealed class ProcessRunner
{
    private readonly TimeSpan _defaultTimeout;

    public ProcessRunner(TimeSpan? defaultTimeout = null)
    {
        _defaultTimeout = defaultTimeout ?? TimeSpan.FromHours(2);
    }

    public async Task<ProcessResult> RunAsync(
        string executablePath,
        string arguments,
        TimeSpan? timeout = null,
        CancellationToken ct = default)
    {
        if (!File.Exists(executablePath))
            throw new FileNotFoundException($"Не найден исполняемый файл 1С: {executablePath}", executablePath);

        var maskedArgs = OneCCommandBuilder.MaskPassword(arguments);
        var commandLine = $"\"{executablePath}\" {arguments}";
        var commandLineMasked = $"\"{executablePath}\" {maskedArgs}";

        var startInfo = new ProcessStartInfo
        {
            FileName = executablePath,
            Arguments = arguments,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = System.Text.Encoding.UTF8,
            StandardErrorEncoding = System.Text.Encoding.UTF8
        };

        using var process = new Process { StartInfo = startInfo };
        var sw = Stopwatch.StartNew();

        process.Start();
        var stdoutTask = process.StandardOutput.ReadToEndAsync(ct);
        var stderrTask = process.StandardError.ReadToEndAsync(ct);

        var effectiveTimeout = timeout ?? _defaultTimeout;
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(effectiveTimeout);

        bool timedOut = false;
        try
        {
            await process.WaitForExitAsync(timeoutCts.Token);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            timedOut = true;
            try
            {
                if (!process.HasExited)
                    process.Kill(entireProcessTree: true);
            }
            catch
            {
                // ignored
            }
        }

        sw.Stop();

        var stdout = await stdoutTask;
        var stderr = await stderrTask;

        return new ProcessResult
        {
            ExitCode = timedOut ? -1 : process.ExitCode,
            StandardOutput = stdout,
            StandardError = stderr,
            Duration = sw.Elapsed,
            CommandLine = commandLine,
            CommandLineMasked = commandLineMasked,
            TimedOut = timedOut
        };
    }
}
