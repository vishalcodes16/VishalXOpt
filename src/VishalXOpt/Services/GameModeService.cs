using System.Threading;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace VishalXOpt.Services;

/// <summary>
/// GameModeX: a one-tap "aggressive gaming profile for this session" toggle. While active it
/// (1) keeps whichever window is currently in the foreground boosted to High priority, following
/// you if you alt-tab between the game and something else, (2) drops a curated list of common
/// background apps to Below Normal, and (3) switches to the Ultimate Performance power plan.
/// Disabling it restores every priority it touched and switches the power plan back to Balanced.
/// </summary>
public sealed class GameModeService : IDisposable
{
    private static readonly string[] BackgroundAppsToThrottle =
    {
        "Discord", "Spotify", "chrome", "msedge", "firefox", "Teams", "slack", "OneDrive", "EpicGamesLauncher"
    };

    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    private readonly PowerCfgService _powerCfg;
    private readonly object _sync = new();
    private Timer? _timer;
    private string? _previousPowerScheme;

    private readonly Dictionary<int, ProcessPriorityClass> _throttledOriginal = new();
    private int? _boostedPid;
    private ProcessPriorityClass _boostedOriginalPriority;

    public bool IsActive { get; private set; }

    public GameModeService(PowerCfgService powerCfg) => _powerCfg = powerCfg;

    public void Enable()
    {
        if (IsActive) return;
        IsActive = true;

        _previousPowerScheme = _powerCfg.GetActiveSchemeGuid();
        _powerCfg.EnableUltimatePerformance();
        lock (_sync) ThrottleBackgroundApps();

        _timer = new Timer(_ => BoostForegroundProcess(), null, TimeSpan.Zero, TimeSpan.FromSeconds(3));
    }

    public void Disable()
    {
        if (!IsActive) return;
        IsActive = false;

        _timer?.Dispose();
        _timer = null;

        lock (_sync)
        {
            RestoreBoosted();
            RestoreThrottled();
        }

        // Put back whatever plan was active before (not blindly "Balanced").
        if (_previousPowerScheme is not null) _powerCfg.SetActiveScheme(_previousPowerScheme);
        else _powerCfg.RestoreBalanced();
        _previousPowerScheme = null;
    }

    private void ThrottleBackgroundApps()
    {
        foreach (var name in BackgroundAppsToThrottle)
        {
            foreach (var process in SafeGetProcessesByName(name))
            {
                try
                {
                    if (!_throttledOriginal.ContainsKey(process.Id))
                        _throttledOriginal[process.Id] = process.PriorityClass;
                    process.PriorityClass = ProcessPriorityClass.BelowNormal;
                }
                catch { /* protected/elevated-differently process - skip */ }
                finally { process.Dispose(); }
            }
        }
    }

    private void RestoreThrottled()
    {
        foreach (var (pid, priority) in _throttledOriginal)
        {
            try
            {
                using var process = Process.GetProcessById(pid);
                process.PriorityClass = priority;
            }
            catch { /* process already exited - nothing to restore */ }
        }
        _throttledOriginal.Clear();
    }

    private void BoostForegroundProcess()
    {
        lock (_sync)
        {
            if (!IsActive) return;
            BoostForegroundProcessCore();
        }
    }

    private void BoostForegroundProcessCore()
    {
        try
        {
            var hwnd = GetForegroundWindow();
            if (hwnd == IntPtr.Zero) return;
            GetWindowThreadProcessId(hwnd, out var pidRaw);
            var pid = (int)pidRaw;
            if (pid == 0 || pid == _boostedPid || _throttledOriginal.ContainsKey(pid)) return;

            using var process = Process.GetProcessById(pid);
            if (process.ProcessName.Equals("VishalXOpt", StringComparison.OrdinalIgnoreCase)) return;

            RestoreBoosted();
            _boostedOriginalPriority = process.PriorityClass;
            process.PriorityClass = ProcessPriorityClass.High;
            _boostedPid = pid;
        }
        catch { /* foreground window belongs to a protected/system process - skip this tick */ }
    }

    private void RestoreBoosted()
    {
        if (_boostedPid is null) return;
        try
        {
            using var process = Process.GetProcessById(_boostedPid.Value);
            process.PriorityClass = _boostedOriginalPriority;
        }
        catch { /* process already exited */ }
        _boostedPid = null;
    }

    private static IEnumerable<Process> SafeGetProcessesByName(string name)
    {
        try { return Process.GetProcessesByName(name); }
        catch { return Enumerable.Empty<Process>(); }
    }

    public void Dispose() => Disable();
}
