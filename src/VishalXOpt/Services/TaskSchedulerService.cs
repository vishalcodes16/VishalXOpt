using VishalXOpt.Models;

namespace VishalXOpt.Services;

/// <summary>
/// Backs the Tasks tab. Windows ships a very large number of built-in scheduled tasks; this is
/// a curated list of the ones tweak guides commonly touch. Enable/disable both go through
/// <c>schtasks.exe</c> (a thin wrapper over the same Task Scheduler COM API a full build would
/// call directly via <c>Microsoft.Win32.TaskScheduler</c> - see the README Implementation Guide).
/// A task that doesn't exist on the current Windows edition/build is simply skipped.
/// </summary>
public sealed class TaskSchedulerService
{
    private static readonly (string DisplayName, string Description, string TaskPath)[] Curated =
    {
        ("Compatibility Appraiser", "Scans installed apps for Windows-upgrade compatibility issues and reports back to Microsoft.",
            @"\Microsoft\Windows\Application Experience\Microsoft Compatibility Appraiser"),
        ("Program Data Updater", "Collects program telemetry used by the compatibility appraiser above.",
            @"\Microsoft\Windows\Application Experience\ProgramDataUpdater"),
        ("CEIP Consolidator", "Uploads Customer Experience Improvement Program usage data.",
            @"\Microsoft\Windows\Customer Experience Improvement Program\Consolidator"),
        ("Disk Diagnostic Data Collector", "Collects S.M.A.R.T./disk-health diagnostic data for Microsoft.",
            @"\Microsoft\Windows\DiskDiagnostic\Microsoft-Windows-DiskDiagnosticDataCollector"),
        ("Windows Error Reporting - Queue Reporting", "Uploads queued crash reports in the background.",
            @"\Microsoft\Windows\Windows Error Reporting\QueueReporting"),
        ("Maps Update Task", "Periodically downloads updated offline map data.",
            @"\Microsoft\Windows\Maps\MapsUpdateTask"),
        ("Maps Toast Task", "Shows notifications suggesting you download offline maps.",
            @"\Microsoft\Windows\Maps\MapsToastTask"),
        ("Feedback - DmClient", "Periodically checks in with the Feedback Hub / diagnostic pipeline.",
            @"\Microsoft\Windows\Feedback\Siuf\DmClient"),
        ("Feedback - DmClientOnScenarioDownload", "Triggers feedback prompts after certain app/scenario downloads.",
            @"\Microsoft\Windows\Feedback\Siuf\DmClientOnScenarioDownload"),
        ("Remote Assistance Task", "Background task supporting Quick Assist / Remote Assistance sessions.",
            @"\Microsoft\Windows\RemoteAssistance\RemoteAssistanceTask"),
    };

    public List<ScheduledTaskInfo> ListTasks()
    {
        // One PowerShell call for the whole curated list. Get-ScheduledTask's State is an enum
        // name ("Ready"/"Running"/"Disabled"), so unlike parsing schtasks.exe text it doesn't break
        // on non-English Windows. Tasks absent from this edition/build simply produce no line.
        var pathsLiteral = string.Join(",", Curated.Select(c => $"'{c.TaskPath}'"));
        var script =
            $"foreach ($p in @({pathsLiteral})) {{ " +
            "$i = $p.LastIndexOf('\\'); " +
            "$t = Get-ScheduledTask -TaskPath ($p.Substring(0,$i+1)) -TaskName ($p.Substring($i+1)) -ErrorAction SilentlyContinue; " +
            "if ($t) { \"$p~~$($t.State)\" } }";

        var result = ProcessRunner.RunPowerShell(script, timeoutMs: 60_000);
        var states = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in result.StandardOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = line.Trim().Split(new[] { "~~" }, StringSplitOptions.None);
            if (parts.Length == 2) states[parts[0]] = parts[1];
        }

        var list = new List<ScheduledTaskInfo>();
        foreach (var (name, description, path) in Curated)
        {
            if (!states.TryGetValue(path, out var state)) continue; // not present on this system
            list.Add(new ScheduledTaskInfo
            {
                DisplayName = name,
                Description = description,
                TaskPath = path,
                IsEnabled = !state.Equals("Disabled", StringComparison.OrdinalIgnoreCase)
            });
        }
        return list;
    }

    public void SetEnabled(ScheduledTaskInfo task, bool enabled) =>
        ProcessRunner.RunAndWait("schtasks.exe", $"/Change /TN \"{task.TaskPath}\" /{(enabled ? "Enable" : "Disable")}");
}
