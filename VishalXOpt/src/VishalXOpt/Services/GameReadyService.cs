using System.Management;
using System.ServiceProcess;
using Microsoft.Win32;

namespace VishalXOpt.Services;

public enum CheckStatus { Pass, Warn, Fail }
public sealed record GameReadyCheck(string Title, string Detail, CheckStatus Status);

/// <summary>Backs the GameReadyX tool: a handful of read-only, safe checks that commonly explain
/// "why is my PC stuttering/updating mid-game" - nothing here changes any system state.</summary>
public sealed class GameReadyService
{
    public List<GameReadyCheck> RunChecks()
    {
        var checks = new List<GameReadyCheck>
        {
            CheckPendingReboot(),
            CheckOldGraphicsDriver(),
            CheckBackgroundAppCount(),
            CheckWindowsUpdateActivity(),
        };
        return checks;
    }

    private static GameReadyCheck CheckPendingReboot()
    {
        bool pending = false;
        try
        {
            using var key1 = Registry.LocalMachine.OpenSubKey(
                @"SYSTEM\CurrentControlSet\Control\Session Manager", writable: false);
            if (key1?.GetValue("PendingFileRenameOperations") is not null) pending = true;

            using var key2 = Registry.LocalMachine.OpenSubKey(
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsUpdate\Auto Update\RebootRequired", writable: false);
            if (key2 is not null) pending = true;

            using var key3 = Registry.LocalMachine.OpenSubKey(
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Component Based Servicing\RebootPending", writable: false);
            if (key3 is not null) pending = true;
        }
        catch { /* leave pending = false if we can't tell */ }

        return pending
            ? new GameReadyCheck("Pending reboot", "Windows has a restart queued up - finish it before a long session; a mid-game forced restart is worse.", CheckStatus.Warn)
            : new GameReadyCheck("Pending reboot", "No restart is pending.", CheckStatus.Pass);
    }

    private static GameReadyCheck CheckOldGraphicsDriver()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT DeviceName, DriverDate FROM Win32_PnPSignedDriver WHERE DeviceClass = 'DISPLAY'");

            DateTime? newest = null;
            string deviceName = "your GPU";
            foreach (ManagementObject obj in searcher.Get())
            {
                var raw = obj["DriverDate"]?.ToString();
                if (raw is null || raw.Length < 8) continue;
                // WMI CIM_DATETIME format: yyyyMMddHHmmss.mmmmmm+UUU
                if (DateTime.TryParseExact(raw[..8], "yyyyMMdd", null,
                        System.Globalization.DateTimeStyles.None, out var date))
                {
                    if (newest is null || date > newest) { newest = date; deviceName = obj["DeviceName"]?.ToString() ?? deviceName; }
                }
            }

            if (newest is null)
                return new GameReadyCheck("Graphics driver age", "Could not read the driver date.", CheckStatus.Warn);

            var ageDays = (DateTime.Now - newest.Value).TotalDays;
            return ageDays switch
            {
                > 365 => new GameReadyCheck("Graphics driver age", $"{deviceName}'s driver is over a year old ({newest:yyyy-MM-dd}). Worth checking for an update.", CheckStatus.Warn),
                > 180 => new GameReadyCheck("Graphics driver age", $"{deviceName}'s driver is {ageDays:0} days old ({newest:yyyy-MM-dd}).", CheckStatus.Warn),
                _ => new GameReadyCheck("Graphics driver age", $"{deviceName}'s driver looks reasonably current ({newest:yyyy-MM-dd}).", CheckStatus.Pass)
            };
        }
        catch
        {
            return new GameReadyCheck("Graphics driver age", "Could not query driver information.", CheckStatus.Warn);
        }
    }

    private static GameReadyCheck CheckBackgroundAppCount()
    {
        try
        {
            var count = System.Diagnostics.Process.GetProcesses()
                .Count(p => SafeHasWindow(p));

            return count switch
            {
                > 15 => new GameReadyCheck("Background apps", $"{count} apps currently have a visible window open - consider closing a few before a competitive session.", CheckStatus.Warn),
                > 8 => new GameReadyCheck("Background apps", $"{count} apps have a visible window open.", CheckStatus.Warn),
                _ => new GameReadyCheck("Background apps", $"Only {count} apps have a visible window open - looks clean.", CheckStatus.Pass)
            };
        }
        catch
        {
            return new GameReadyCheck("Background apps", "Could not enumerate processes.", CheckStatus.Warn);
        }
    }

    private static bool SafeHasWindow(System.Diagnostics.Process p)
    {
        try { return p.MainWindowHandle != IntPtr.Zero; }
        catch { return false; }
    }

    private static GameReadyCheck CheckWindowsUpdateActivity()
    {
        try
        {
            using var sc = new ServiceController("wuauserv");
            return sc.Status == ServiceControllerStatus.Running
                ? new GameReadyCheck("Windows Update", "The Update service is currently running - it may be downloading/installing in the background. Consider pausing updates from the Basic tab before a session.", CheckStatus.Warn)
                : new GameReadyCheck("Windows Update", "The Update service is idle right now.", CheckStatus.Pass);
        }
        catch
        {
            return new GameReadyCheck("Windows Update", "Could not read the Update service state.", CheckStatus.Warn);
        }
    }
}
