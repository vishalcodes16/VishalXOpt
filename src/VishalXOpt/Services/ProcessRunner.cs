using System.Diagnostics;
using System.IO;
using System.Text;

namespace VishalXOpt.Services;

public sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError);

/// <summary>
/// Every tab that isn't a plain registry flip (Firewall via netsh, Components via dism,
/// Tasks via schtasks, Network adapters and Debloat via PowerShell cmdlets, the WinUtil tab)
/// ultimately goes through this one class, so there is exactly one place that knows how to
/// launch a hidden, elevated helper process and capture its output.
/// </summary>
public static class ProcessRunner
{
    public static ProcessResult RunAndWait(string fileName, string arguments, int timeoutMs = 60_000)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            }
        };

        var stdout = new StringBuilder();
        var stderr = new StringBuilder();
        process.OutputDataReceived += (_, e) => { if (e.Data is not null) stdout.AppendLine(e.Data); };
        process.ErrorDataReceived += (_, e) => { if (e.Data is not null) stderr.AppendLine(e.Data); };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        if (!process.WaitForExit(timeoutMs))
        {
            try { process.Kill(entireProcessTree: true); } catch { /* ignore */ }
            return new ProcessResult(-1, stdout.ToString(), "Timed out.");
        }

        return new ProcessResult(process.ExitCode, stdout.ToString(), stderr.ToString());
    }

    /// <summary>
    /// Writes <paramref name="script"/> to a temp .ps1 file and runs it with <c>-File</c>, rather
    /// than passing it inline via <c>-Command "..."</c>. A script can itself contain quotes,
    /// braces and interpolation syntax (several in this codebase do - see TaskSchedulerService),
    /// and escaping all of that correctly for both PowerShell's parser AND the Windows
    /// command-line argument parser at the same time is exactly the kind of thing that's easy to
    /// get subtly wrong. Writing the script to a file sidesteps that whole problem: the file's
    /// content needs no shell escaping at all, and the only thing quoted on the command line is
    /// the temp file's own path (always a plain, space-safe GUID-based name).
    /// </summary>
    public static ProcessResult RunPowerShell(string script, int timeoutMs = 120_000)
    {
        var tempScript = Path.Combine(Path.GetTempPath(), $"vishalxopt-{Guid.NewGuid():N}.ps1");
        try
        {
            File.WriteAllText(tempScript, script);
            return RunAndWait("powershell.exe", $"-NoProfile -ExecutionPolicy Bypass -File \"{tempScript}\"", timeoutMs);
        }
        finally
        {
            try { File.Delete(tempScript); } catch { /* best-effort cleanup */ }
        }
    }

    /// <summary>Runs a command and streams each output line to <paramref name="onLine"/> as it
    /// arrives - used by the WinUtil tab so the person can watch the bootstrap script run live
    /// instead of staring at a frozen window.</summary>
    public static async Task<int> RunStreamingAsync(string fileName, string arguments, Action<string> onLine, CancellationToken ct = default)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            },
            EnableRaisingEvents = true
        };

        process.OutputDataReceived += (_, e) => { if (e.Data is not null) onLine(e.Data); };
        process.ErrorDataReceived += (_, e) => { if (e.Data is not null) onLine("[stderr] " + e.Data); };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        try
        {
            await process.WaitForExitAsync(ct);
        }
        catch (OperationCanceledException)
        {
            // "Stop" must actually stop the thing it started, not just stop listening to it.
            try { process.Kill(entireProcessTree: true); } catch { /* already exited */ }
            throw;
        }
        return process.ExitCode;
    }
}
