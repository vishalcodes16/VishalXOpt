using VishalXOpt.Models;

namespace VishalXOpt.Services;

/// <summary>
/// Backs the Components tab: a curated slice of Windows Optional Features, the natural
/// companion to Debloat for removing unused OS pieces rather than just apps. Uses the
/// <c>WindowsOptionalFeature</c> PowerShell cmdlets (a friendlier wrapper over the same DISM
/// API the README's Implementation Guide describes).
/// </summary>
public sealed class DismComponentService
{
    private static readonly (string DisplayName, string FeatureName)[] Curated =
    {
        (".NET Framework 3.5", "NetFx3"),
        ("Hyper-V", "Microsoft-Hyper-V-All"),
        ("Windows Media Player (legacy)", "WindowsMediaPlayer"),
        ("Print to PDF", "Printing-PrintToPDFServices-Features"),
        ("Legacy Components (DirectPlay)", "LegacyComponents"),
        ("Telnet Client", "TelnetClient"),
        ("Work Folders Client", "WorkFolders-Client"),
        ("Windows Sandbox", "Containers-DisposableClientVM"),
        ("Windows Subsystem for Linux", "Microsoft-Windows-Subsystem-Linux"),
    };

    public List<WindowsFeatureInfo> ListFeatures()
    {
        const string script =
            "Get-WindowsOptionalFeature -Online | ForEach-Object { \"$($_.FeatureName)|$($_.State)\" }";
        var result = ProcessRunner.RunPowerShell(script, timeoutMs: 45_000);

        var states = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in result.StandardOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = line.Trim().Split('|');
            if (parts.Length == 2) states[parts[0]] = parts[1];
        }

        var list = new List<WindowsFeatureInfo>();
        foreach (var (displayName, featureName) in Curated)
        {
            if (!states.TryGetValue(featureName, out var state)) continue; // not on this SKU/build
            list.Add(new WindowsFeatureInfo { DisplayName = displayName, FeatureName = featureName, State = state });
        }
        return list;
    }

    public void SetEnabled(WindowsFeatureInfo feature, bool enabled)
    {
        var script = enabled
            ? $"Enable-WindowsOptionalFeature -Online -FeatureName '{feature.FeatureName}' -All -NoRestart -ErrorAction SilentlyContinue"
            : $"Disable-WindowsOptionalFeature -Online -FeatureName '{feature.FeatureName}' -NoRestart -ErrorAction SilentlyContinue";
        ProcessRunner.RunPowerShell(script, timeoutMs: 180_000);
    }

    /// <summary>The "Reserved Storage" toggle mentioned on the Tweaks tab in the README lives
    /// here too since it is also a DISM-backed, whole-system setting.</summary>
    public void SetReservedStorage(bool enabled) =>
        ProcessRunner.RunAndWait("Dism.exe", $"/Online /Set-ReservedStorageState /State:{(enabled ? "Enabled" : "Disabled")}", timeoutMs: 60_000);
}
