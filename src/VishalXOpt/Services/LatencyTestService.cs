using System.Diagnostics;

namespace VishalXOpt.Services;

public sealed record LatencyTestResult(double AvgGapMs, double MaxGapMs, int SpikesOver2Ms, int SampleCount);

/// <summary>
/// A genuine, if approximate, latency measurement: it repeatedly asks the OS scheduler for a
/// 1ms sleep and measures how late it actually wakes up. Large/variable gaps correlate with the
/// same underlying causes true DPC/ISR latency tools (LatencyMon and friends) report - driver
/// interrupt storms, power-saving transitions, thermal throttling - but this is a scheduler-level
/// proxy, not a kernel ETW trace of individual ISR/DPC routines. That would need the
/// <c>Microsoft.Diagnostics.Tracing.TraceEvent</c> package and an NT Kernel Logger session; it's
/// a reasonable v1.1 upgrade path but is a materially bigger, harder-to-verify addition than this
/// starter build takes on. The label in the UI says "scheduler jitter test" for exactly this
/// reason - it does not claim to be a DPC/ISR breakdown.
/// </summary>
public sealed class LatencyTestService
{
    public LatencyTestResult RunSchedulerJitterTest(int sampleWindowMs = 3000)
    {
        var gaps = new List<double>();
        var sw = Stopwatch.StartNew();
        var thread = Thread.CurrentThread;
        var originalPriority = thread.Priority;

        try
        {
            thread.Priority = ThreadPriority.Highest;
            while (sw.ElapsedMilliseconds < sampleWindowMs)
            {
                var before = sw.Elapsed.TotalMilliseconds;
                Thread.Sleep(1);
                var after = sw.Elapsed.TotalMilliseconds;
                // Expected gap is ~1ms; anything beyond that is scheduler-induced delay.
                gaps.Add(Math.Max(0, after - before - 1.0));
            }
        }
        finally
        {
            thread.Priority = originalPriority;
        }

        if (gaps.Count == 0) return new LatencyTestResult(0, 0, 0, 0);

        return new LatencyTestResult(
            AvgGapMs: gaps.Average(),
            MaxGapMs: gaps.Max(),
            SpikesOver2Ms: gaps.Count(g => g > 2.0),
            SampleCount: gaps.Count);
    }
}
