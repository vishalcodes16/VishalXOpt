using System.Diagnostics;
using System.IO;
using System.Net.NetworkInformation;
using System.Threading;

namespace VishalXOpt.Services;

public sealed record InternetTestResult(double AvgLatencyMs, double JitterMs, double PacketLossPercent);
public sealed record BottleneckResult(float CpuPercent, float GpuPercent, string Verdict);

/// <summary>
/// Backs three of the Tools-grid entries: GodMode, Internet test, and Bottleneck. The other six
/// (StoreX, GameModeX, ProcessX, PC Latency Test, GameReadyX, Steam) are fully implemented too,
/// each in its own service - see <c>StoreXService</c>, <c>GameModeService</c>,
/// <c>ProcessAutomationService</c>, <c>LatencyTestService</c>, <c>GameReadyService</c> and
/// <c>SteamService</c>.
/// </summary>
public sealed class ToolsService
{
    /// <summary>Creates the classic hidden Windows "GodMode" folder on the Desktop - a folder
    /// named with this exact CLSID suffix is recognized by Explorer as a shortcut to every
    /// Control Panel setting in one view. Long-public, documented Windows shell trick.</summary>
    public string CreateGodModeFolder()
    {
        var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        var path = Path.Combine(desktop, "GodMode.{ED7BA470-8E54-465E-825C-99712043E01C}");
        if (!Directory.Exists(path)) Directory.CreateDirectory(path);
        return path;
    }

    public async Task<InternetTestResult> RunInternetTestAsync(string host = "1.1.1.1", int pings = 10)
    {
        var times = new List<double>();
        int lost = 0;

        using var ping = new Ping();
        for (int i = 0; i < pings; i++)
        {
            try
            {
                var reply = await ping.SendPingAsync(host, 1500);
                if (reply.Status == IPStatus.Success) times.Add(reply.RoundtripTime);
                else lost++;
            }
            catch
            {
                lost++;
            }
        }

        if (times.Count == 0) return new InternetTestResult(0, 0, 100);

        var avg = times.Average();
        var jitter = times.Count > 1
            ? Math.Sqrt(times.Sum(t => Math.Pow(t - avg, 2)) / times.Count)
            : 0;
        var lossPercent = lost * 100.0 / pings;

        return new InternetTestResult(avg, jitter, lossPercent);
    }

    /// <summary>Samples the same counters Task Manager's own Performance tab graphs use.</summary>
    public BottleneckResult SampleBottleneck()
    {
        float cpu = 0, gpu = 0;

        try
        {
            using var cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
            cpuCounter.NextValue();
            Thread.Sleep(500); // PerformanceCounter needs two samples to report a real value
            cpu = cpuCounter.NextValue();
        }
        catch { /* counters unavailable - leave at 0 */ }

        var gpuCounters = new List<PerformanceCounter>();
        try
        {
            var category = new PerformanceCounterCategory("GPU Engine");
            foreach (var instance in category.GetInstanceNames()
                         .Where(n => n.Contains("engtype_3D", StringComparison.OrdinalIgnoreCase)))
            {
                try { gpuCounters.Add(new PerformanceCounter("GPU Engine", "Utilization Percentage", instance)); }
                catch { /* instance vanished between listing and opening (a process exited) */ }
            }

            foreach (var c in gpuCounters) c.NextValue();
            Thread.Sleep(500);
            foreach (var c in gpuCounters)
            {
                try { gpu += c.NextValue(); } catch { /* instance gone */ }
            }
            gpu = Math.Min(gpu, 100f);
        }
        catch { /* "GPU Engine" counters unavailable (older Windows / no WDDM 2.0 GPU) - leave at 0 */ }
        finally
        {
            foreach (var c in gpuCounters) c.Dispose();
        }

        string verdict = cpu <= 0 && gpu <= 0
            ? "Not enough data - try again while a game/benchmark is running."
            : cpu > gpu + 10
                ? "Likely CPU-bound right now."
                : gpu > cpu + 10
                    ? "Likely GPU-bound right now."
                    : "CPU and GPU are fairly balanced right now.";

        return new BottleneckResult(cpu, gpu, verdict);
    }
}
