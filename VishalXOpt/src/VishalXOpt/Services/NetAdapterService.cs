using VishalXOpt.Models;

namespace VishalXOpt.Services;

/// <summary>
/// NIC "advanced property" keywords are vendor-specific, so rather than hardcoding registry
/// paths per OEM this wraps the same <c>NetAdapter</c> PowerShell cmdlets Device Manager's own
/// Advanced tab is built on.
/// </summary>
public sealed class NetAdapterService
{
    // A commonly-offered "Low Latency" property set - disables power-saving/offload behavior
    // that adds a small amount of latency in exchange for lower idle power draw. Not every
    // adapter exposes every property below; unsupported ones are silently skipped.
    private static readonly (string DisplayName, string LowLatencyValue)[] LowLatencyProperties =
    {
        ("Sleep on WoWLAN Disconnect", "Disabled"),
        ("Packet Coalescing", "Disabled"),
        ("Interrupt Moderation", "Disabled"),
        ("Energy-Efficient Ethernet", "Disabled"),
        ("Green Ethernet", "Disabled"),
    };

    public List<string> ListAdapterNames()
    {
        var result = ProcessRunner.RunPowerShell("Get-NetAdapter | Select-Object -ExpandProperty Name");
        return result.StandardOutput
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.Trim())
            .Where(l => l.Length > 0)
            .ToList();
    }

    public List<NetAdapterPropertyInfo> ListProperties(string adapterName)
    {
        // "~~" between fields, ";;" between ValidDisplayValues entries - neither combination is
        // realistic to see inside an actual adapter property's display text.
        var script =
            $"Get-NetAdapterAdvancedProperty -Name '{adapterName}' -ErrorAction SilentlyContinue | " +
            "ForEach-Object { \"$($_.DisplayName)~~$($_.DisplayValue)~~$($_.RegistryKeyword)~~$($_.ValidDisplayValues -join ';;')\" }";

        var result = ProcessRunner.RunPowerShell(script);
        var list = new List<NetAdapterPropertyInfo>();

        foreach (var line in result.StandardOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = line.Trim().Split(new[] { "~~" }, StringSplitOptions.None);
            if (parts.Length < 2) continue;

            var validValues = parts.Length > 3 && parts[3].Length > 0
                ? parts[3].Split(new[] { ";;" }, StringSplitOptions.RemoveEmptyEntries).ToList()
                : new List<string>();

            // Always make sure the adapter's current value is itself a selectable option, even
            // if the driver didn't report it back in ValidDisplayValues for some reason.
            if (validValues.Count > 0 && !validValues.Contains(parts[1], StringComparer.OrdinalIgnoreCase))
                validValues.Insert(0, parts[1]);

            list.Add(new NetAdapterPropertyInfo
            {
                AdapterName = adapterName,
                DisplayName = parts[0],
                DisplayValue = parts[1],
                RegistryKeyword = parts.Length > 2 ? parts[2] : "",
                DefaultValue = parts[1],
                ValidDisplayValues = validValues
            });
        }

        return list;
    }

    public void SetProperty(string adapterName, string displayName, string displayValue) =>
        ProcessRunner.RunPowerShell(
            $"Set-NetAdapterAdvancedProperty -Name '{adapterName}' -DisplayName '{displayName}' " +
            $"-DisplayValue '{displayValue}' -ErrorAction SilentlyContinue");

    /// <summary>Applies the curated "Low Latency" preset to every supported property on the
    /// given adapter.</summary>
    public void ApplyLowLatencyPreset(string adapterName)
    {
        foreach (var (displayName, value) in LowLatencyProperties)
            SetProperty(adapterName, displayName, value);
    }
}
