using System.Threading;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using VishalXOpt.Models;

namespace VishalXOpt.Services;

/// <summary>
/// Backs the ProcessX tool: a small rule engine that periodically walks the running-process list
/// and applies a saved <see cref="ProcessPriorityClass"/> to anything matching a rule's process
/// name. GameModeX (see <see cref="GameModeService"/>) is built on top of the same engine rather
/// than duplicating process-priority logic.
/// </summary>
public sealed class ProcessAutomationService : IDisposable
{
    private readonly List<ProcessRule> _rules = new();
    private Timer? _timer;
    private readonly object _lock = new();

    public IReadOnlyList<ProcessRule> Rules => _rules;
    public bool IsRunning { get; private set; }

    private static string ConfigPath
    {
        get
        {
            var folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "VishalXOpt");
            Directory.CreateDirectory(folder);
            return Path.Combine(folder, "process-rules.json");
        }
    }

    public ProcessAutomationService()
    {
        Load();
    }

    private void Load()
    {
        try
        {
            if (!File.Exists(ConfigPath)) return;
            var saved = JsonSerializer.Deserialize<List<ProcessRule>>(File.ReadAllText(ConfigPath));
            if (saved is not null) _rules.AddRange(saved);
        }
        catch { /* start with an empty rule set if the file is corrupt */ }
    }

    private void Save()
    {
        try { File.WriteAllText(ConfigPath, JsonSerializer.Serialize(_rules)); }
        catch { /* best-effort persistence */ }
    }

    public void AddRule(string processName, ProcessPriorityClass priority)
    {
        lock (_lock)
        {
            _rules.RemoveAll(r => r.ProcessName.Equals(processName, StringComparison.OrdinalIgnoreCase));
            _rules.Add(new ProcessRule { ProcessName = processName, Priority = priority, Enabled = true });
        }
        Save();
    }

    public void RemoveRule(ProcessRule rule)
    {
        lock (_lock) { _rules.Remove(rule); }
        Save();
    }

    public void SetRuleEnabled(ProcessRule rule, bool enabled)
    {
        rule.Enabled = enabled;
        Save();
    }

    /// <summary>Applies every enabled rule once, against the processes running right now.
    /// Safe to call repeatedly (e.g. from a UI button) even while the background timer is also
    /// running - each call is independent and just re-applies the current rule set.</summary>
    public void ApplyRulesOnce()
    {
        List<ProcessRule> snapshot;
        lock (_lock) { snapshot = _rules.Where(r => r.Enabled).ToList(); }
        if (snapshot.Count == 0) return;

        foreach (var process in Process.GetProcesses())
        {
            try
            {
                var rule = snapshot.FirstOrDefault(r => r.ProcessName.Equals(process.ProcessName, StringComparison.OrdinalIgnoreCase));
                if (rule is not null && process.PriorityClass != rule.Priority)
                    process.PriorityClass = rule.Priority;
            }
            catch { /* access denied (elevated/protected process) - skip it */ }
            finally { process.Dispose(); }
        }
    }

    /// <summary>Starts a background timer that calls <see cref="ApplyRulesOnce"/> every
    /// <paramref name="intervalSeconds"/> seconds, so a game launched after you turned automation
    /// on still gets its rule applied.</summary>
    public void Start(int intervalSeconds = 5)
    {
        if (IsRunning) return;
        IsRunning = true;
        _timer = new Timer(_ => ApplyRulesOnce(), null, TimeSpan.Zero, TimeSpan.FromSeconds(intervalSeconds));
    }

    public void Stop()
    {
        IsRunning = false;
        _timer?.Dispose();
        _timer = null;
    }

    public void Dispose() => Stop();
}
