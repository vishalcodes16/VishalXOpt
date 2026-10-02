using System.ServiceProcess;
using Microsoft.Win32;

namespace VishalXOpt.Services;

/// <summary>
/// Reads/writes the state behind tweaks of kind <c>Service</c> (SysMain, Windows Search, Print
/// Spooler, Delivery Optimization, DiagTrack, dmwappushsvc, WerSvc, ...).
///
/// Semantics match every other tweak in the catalog: "applied" (ON) means the optimization is in
/// effect - here, that the service is <b>Disabled</b> (and stopped). "Not applied" (OFF) puts the
/// service back to its Windows-default start mode, supplied by the catalog entry.
/// </summary>
public sealed class ServiceControlService
{
    /// <summary>The current start mode as an <c>sc config start=</c> value ("auto",
    /// "delayed-auto", "demand", "disabled"), or null if the service doesn't exist on this PC.</summary>
    public string? ReadStartMode(string serviceName)
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey($@"SYSTEM\CurrentControlSet\Services\{serviceName}");
            if (key?.GetValue("Start") is not int start) return null;

            return start switch
            {
                2 => key.GetValue("DelayedAutostart") is int d && d == 1 ? "delayed-auto" : "auto",
                3 => "demand",
                4 => "disabled",
                0 => "boot",
                1 => "system",
                _ => "demand"
            };
        }
        catch
        {
            return null;
        }
    }

    /// <summary>True = the optimization is applied (service is Disabled). Null = service not present.</summary>
    public bool? ReadIsApplied(string serviceName)
    {
        var mode = ReadStartMode(serviceName);
        return mode is null ? null : mode == "disabled";
    }

    /// <returns>The start mode the service had <em>before</em> this call, for Undo.</returns>
    public string? Apply(string serviceName, bool applied, string defaultStartMode)
    {
        var previous = ReadStartMode(serviceName);
        if (previous is null) return null; // service not present on this edition/build

        if (applied)
        {
            SetStartMode(serviceName, "disabled");
            TryStop(serviceName);
        }
        else
        {
            SetStartMode(serviceName, defaultStartMode);
        }
        return previous;
    }

    /// <summary>Restores a start mode captured by <see cref="Apply"/> (used by Undo).</summary>
    public void Restore(string serviceName, string previousStartMode)
    {
        SetStartMode(serviceName, previousStartMode);
        if (previousStartMode is "auto" or "delayed-auto")
        {
            try
            {
                using var sc = new ServiceController(serviceName);
                if (sc.Status == ServiceControllerStatus.Stopped) sc.Start();
            }
            catch { /* best-effort: some services can only start on demand/trigger */ }
        }
    }

    private static void TryStop(string serviceName)
    {
        try
        {
            using var sc = new ServiceController(serviceName);
            if (sc.Status is ServiceControllerStatus.Running or ServiceControllerStatus.StartPending)
            {
                sc.Stop();
                sc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(8));
            }
        }
        catch { /* protected service or has dependents - the Disabled start type still takes effect at next boot */ }
    }

    // ServiceController has no built-in "set start type" API - the supported route is the sc.exe
    // CLI (which edits HKLM\SYSTEM\CurrentControlSet\Services\<name>\Start for us, including the
    // DelayedAutostart flag that a raw registry write would have to manage by hand).
    private static void SetStartMode(string serviceName, string mode)
    {
        var scMode = mode switch
        {
            "auto" or "delayed-auto" or "demand" or "disabled" => mode,
            "boot" => "boot",
            "system" => "system",
            _ => "demand"
        };
        ProcessRunner.RunAndWait("sc.exe", $"config \"{serviceName}\" start= {scMode}");
    }
}
