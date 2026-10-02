using System.Text.RegularExpressions;

namespace VishalXOpt.Services;

/// <summary>Wraps <c>powercfg.exe</c> for the Power Management tab and GameModeX.</summary>
public sealed class PowerCfgService
{
    // Microsoft's fixed GUID for the hidden Ultimate Performance scheme *template*. Duplicating it
    // creates a brand-new scheme with a NEW random GUID, so we must never /setactive this constant
    // directly - we look up (or capture) the duplicate's GUID instead.
    private const string UltimatePerformanceTemplateGuid = "e9a42b02-d5df-448d-aa00-03f14749eb61";
    private const string BalancedGuid = "381b4222-f694-41f0-9685-ff5bb260df2e";

    private static readonly Regex GuidRegex = new(
        "[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}", RegexOptions.Compiled);

    public string GetActiveSchemeName()
    {
        var result = ProcessRunner.RunAndWait("powercfg.exe", "/getactivescheme");
        return result.StandardOutput.Trim();
    }

    public string? GetActiveSchemeGuid()
    {
        var result = ProcessRunner.RunAndWait("powercfg.exe", "/getactivescheme");
        var match = GuidRegex.Match(result.StandardOutput);
        return match.Success ? match.Value : null;
    }

    public void SetActiveScheme(string guid) =>
        ProcessRunner.RunAndWait("powercfg.exe", $"/setactive {guid}");

    /// <summary>Finds an existing Ultimate Performance scheme in the plan list, if there is one.
    /// Matches either the template GUID itself or a plan whose name contains "Ultimate".</summary>
    private static string? FindExistingUltimateScheme()
    {
        var list = ProcessRunner.RunAndWait("powercfg.exe", "/list").StandardOutput;
        foreach (var line in list.Split('\n'))
        {
            var match = GuidRegex.Match(line);
            if (!match.Success) continue;
            if (match.Value.Equals(UltimatePerformanceTemplateGuid, StringComparison.OrdinalIgnoreCase) ||
                line.Contains("Ultimate", StringComparison.OrdinalIgnoreCase))
                return match.Value;
        }
        return null;
    }

    public bool IsUltimatePerformanceAvailable() => FindExistingUltimateScheme() is not null;

    /// <returns>true if an Ultimate Performance scheme was found/created and activated.
    /// Some hardware (notably Modern Standby laptops) doesn't expose the scheme at all.</returns>
    public bool EnableUltimatePerformance()
    {
        var guid = FindExistingUltimateScheme();
        if (guid is null)
        {
            var duplicate = ProcessRunner.RunAndWait("powercfg.exe", $"/duplicatescheme {UltimatePerformanceTemplateGuid}");
            var match = GuidRegex.Match(duplicate.StandardOutput);
            guid = match.Success ? match.Value : null;
        }

        if (guid is null) return false;
        SetActiveScheme(guid);
        return true;
    }

    public void RestoreBalanced() => SetActiveScheme(BalancedGuid);

    /// <summary>Sets the minimum processor state (%) for the active plan on AC power.
    /// 100 avoids CPU park/ramp stalls; the Windows default is typically 5.</summary>
    public void SetMinProcessorState(int percent)
    {
        ProcessRunner.RunAndWait("powercfg.exe", $"/setacvalueindex scheme_current sub_processor PROCTHROTTLEMIN {percent}");
        ProcessRunner.RunAndWait("powercfg.exe", "/setactive scheme_current");
    }

    public void DisableUsbSelectiveSuspend()
    {
        ProcessRunner.RunAndWait("powercfg.exe", "/setacvalueindex scheme_current 2a737441-1930-4402-8d77-b2bebba308a3 48e6b7a6-50f5-4782-a5d4-53bb8f07e226 0");
        ProcessRunner.RunAndWait("powercfg.exe", "/setactive scheme_current");
    }
}
