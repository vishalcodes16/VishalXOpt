namespace VishalXOpt.Models;

/// <summary>A PnP device as shown on the Devices / Interrupts tabs.</summary>
public sealed class DeviceInfo
{
    public string Name { get; set; } = "";
    public string PnpDeviceId { get; set; } = "";
    public string RegistryEnumPath => $@"SYSTEM\CurrentControlSet\Enum\{PnpDeviceId}\Device Parameters\Interrupt Management";
    public bool MsiModeSupported { get; set; }
    public bool MsiModeEnabled { get; set; }
    public bool IsCommonlyUnnecessary { get; set; }
    /// <summary>True when Windows already reports the device as disabled (CM problem code 22).</summary>
    public bool IsDisabled { get; set; }
    public int[] AffinityThreads { get; set; } = Array.Empty<int>();
}

/// <summary>One advanced driver property row on the Network adapters tab.</summary>
public sealed class NetAdapterPropertyInfo
{
    public string AdapterName { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string DisplayValue { get; set; } = "";
    public string RegistryKeyword { get; set; } = "";
    public string LowLatencyValue { get; set; } = "";
    public string DefaultValue { get; set; } = "";
    /// <summary>Every value the driver reports as acceptable for this property (from
    /// <c>Get-NetAdapterAdvancedProperty</c>'s <c>ValidDisplayValues</c>). Empty for properties
    /// that only expose a free-form value.</summary>
    public List<string> ValidDisplayValues { get; set; } = new();
}

/// <summary>A built-in scheduled task row on the Tasks tab.</summary>
public sealed class ScheduledTaskInfo
{
    public string DisplayName { get; set; } = "";
    public string Description { get; set; } = "";

    /// <summary>Full Task Scheduler path, e.g. <c>\Microsoft\Windows\Application Experience\Microsoft Compatibility Appraiser</c>.</summary>
    public string TaskPath { get; set; } = "";
    public bool IsEnabled { get; set; }
}

/// <summary>A Windows optional feature row on the Components tab (DISM-backed).</summary>
public sealed class WindowsFeatureInfo
{
    public string DisplayName { get; set; } = "";
    public string FeatureName { get; set; } = ""; // DISM /FeatureName value
    public string State { get; set; } = "";        // Enabled / Disabled
}

/// <summary>Where an autostart entry lives.</summary>
public enum AutorunSourceKind
{
    RegistryRun,
    StartupFolder
}

/// <summary>One autostart entry on the Autoruns tab (Run keys, or a file in the Startup folder).</summary>
public sealed class AutorunEntryInfo
{
    public string Name { get; set; } = "";
    public string Command { get; set; } = "";
    public AutorunSourceKind Source { get; set; } = AutorunSourceKind.RegistryRun;

    // --- Source == RegistryRun ---
    public RegistryHiveKind Hive { get; set; }
    public string RegistryPath { get; set; } = "";

    // --- Source == StartupFolder ---
    /// <summary>Full path to the .lnk/.exe/.bat file in the Startup folder.</summary>
    public string FilePath { get; set; } = "";
    public bool IsAllUsersStartup { get; set; }

    public bool IsEnabled { get; set; } = true;
}

/// <summary>A removable UWP/AppX package row on the Debloat tab.</summary>
public sealed class AppxPackageInfo
{
    public string Name { get; set; } = "";
    public string PackageFullName { get; set; } = "";
    public double SizeMb { get; set; }
}
