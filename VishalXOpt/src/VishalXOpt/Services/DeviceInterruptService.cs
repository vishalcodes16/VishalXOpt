using System.IO;
using System.Management;
using System.Text.Json;
using Microsoft.Win32;
using VishalXOpt.Models;

namespace VishalXOpt.Services;

/// <summary>
/// Backs the combined "Devices &amp; Interrupts" tab. Both MSI Mode and IRQ affinity live under
/// the same documented registry node for each device:
/// <c>HKLM\SYSTEM\CurrentControlSet\Enum\&lt;PNPDeviceID&gt;\Device Parameters\Interrupt Management\...</c>
/// See the README's Implementation Guide, section 3, for the exact value names.
/// </summary>
public sealed class DeviceInterruptService
{
    // Keep the list short and relevant - GPU, network, USB host controllers, audio and storage
    // are the device classes that actually matter for interrupt/MSI tuning.
    private static readonly string[] InterestingClasses = { "Display", "Net", "HDC", "Media", "SCSIAdapter" };

    private static readonly string[] CommonlyUnnecessaryNames =
    {
        "Composite Bus Enumerator", "Microsoft Virtual Drive Enumerator"
    };

    public List<DeviceInfo> ListDevices()
    {
        var devices = new List<DeviceInfo>();
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT Name, DeviceID, PNPClass, ConfigManagerErrorCode FROM Win32_PnPEntity");
            foreach (ManagementObject obj in searcher.Get())
            {
                var pnpClass = obj["PNPClass"]?.ToString() ?? "";
                var name = obj["Name"]?.ToString() ?? "(unknown device)";
                var deviceId = obj["DeviceID"]?.ToString() ?? "";
                if (string.IsNullOrEmpty(deviceId)) continue;

                bool interesting = InterestingClasses.Contains(pnpClass, StringComparer.OrdinalIgnoreCase);
                bool unnecessary = CommonlyUnnecessaryNames.Any(n => name.Contains(n, StringComparison.OrdinalIgnoreCase));
                if (!interesting && !unnecessary) continue;

                var device = new DeviceInfo
                {
                    Name = name,
                    PnpDeviceId = deviceId,
                    IsCommonlyUnnecessary = unnecessary,
                    // Config Manager problem code 22 = "device is disabled"
                    IsDisabled = obj["ConfigManagerErrorCode"] is uint code && code == 22
                };
                ReadMsiState(device);
                devices.Add(device);
            }
        }
        catch { /* WMI unavailable / access denied - return whatever we found */ }

        return devices.OrderBy(d => d.Name).ToList();
    }

    private void ReadMsiState(DeviceInfo device)
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(
                $@"{device.RegistryEnumPath}\MessageSignaledInterruptProperties", writable: false);
            var raw = key?.GetValue("MSISupported");
            if (raw is int i)
            {
                device.MsiModeSupported = true;
                device.MsiModeEnabled = i != 0;
            }
        }
        catch { /* leave MsiModeSupported = false */ }
    }

    /// <summary>Enables/disables MSI mode. NOTE: writing MSISupported=1 only has an effect if
    /// the device's own driver actually implements MSI - it is a request, not a guarantee.</summary>
    public void SetMsiMode(DeviceInfo device, bool enabled)
    {
        using var key = Registry.LocalMachine.CreateSubKey(
            $@"{device.RegistryEnumPath}\MessageSignaledInterruptProperties", writable: true);
        key?.SetValue("MSISupported", enabled ? 1 : 0, RegistryValueKind.DWord);
    }

    public void DisableDevice(DeviceInfo device) =>
        ProcessRunner.RunAndWait("pnputil.exe", $"/disable-device \"{device.PnpDeviceId}\"");

    public void EnableDevice(DeviceInfo device) =>
        ProcessRunner.RunAndWait("pnputil.exe", $"/enable-device \"{device.PnpDeviceId}\"");

    /// <summary>Pins a device's interrupts to the given zero-based logical-processor indices.
    /// DevicePolicy = 5 is "IrqPolicyExplicitAffinity" (Microsoft WDK, "IRQ Affinity Policies").</summary>
    public void SetInterruptAffinity(DeviceInfo device, IReadOnlyCollection<int> logicalThreadIndices)
    {
        using var key = Registry.LocalMachine.CreateSubKey(
            $@"{device.RegistryEnumPath}\Affinity Policy", writable: true);
        if (key is null) return;

        key.SetValue("DevicePolicy", 5, RegistryValueKind.DWord);

        ulong mask = 0;
        foreach (var thread in logicalThreadIndices)
            if (thread is >= 0 and < 64) mask |= 1UL << thread;

        key.SetValue("AssignmentSetOverride", BitConverter.GetBytes(mask), RegistryValueKind.Binary);
    }

    public void ClearInterruptAffinity(DeviceInfo device)
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(
                $@"{device.RegistryEnumPath}\Affinity Policy", writable: true);
            key?.DeleteValue("DevicePolicy", throwOnMissingValue: false);
            key?.DeleteValue("AssignmentSetOverride", throwOnMissingValue: false);
        }
        catch { /* nothing to clear */ }
    }

    // ---------------------------------------------------------------------------------------
    // "Lock Interrupt Routing": remember every affinity the person pinned while the lock was on,
    // and re-apply them each time Vishal X Opt starts. This is deliberately an on-launch re-apply,
    // not a background Windows service - if a driver update resets a device's affinity, opening
    // the app puts it back. (A true always-on guard would need its own service project.)
    // ---------------------------------------------------------------------------------------

    private sealed class LockConfig
    {
        public bool Enabled { get; set; }
        public Dictionary<string, int[]> Affinities { get; set; } = new();
    }

    private static string LockFilePath
    {
        get
        {
            var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "VishalXOpt");
            Directory.CreateDirectory(folder);
            return Path.Combine(folder, "locked-interrupts.json");
        }
    }

    private static LockConfig LoadLockConfig()
    {
        try
        {
            return File.Exists(LockFilePath)
                ? JsonSerializer.Deserialize<LockConfig>(File.ReadAllText(LockFilePath)) ?? new LockConfig()
                : new LockConfig();
        }
        catch { return new LockConfig(); }
    }

    private static void SaveLockConfig(LockConfig config)
    {
        try { File.WriteAllText(LockFilePath, JsonSerializer.Serialize(config)); }
        catch { /* best-effort persistence */ }
    }

    public bool IsLockEnabled() => LoadLockConfig().Enabled;

    public void SetLockEnabled(bool enabled)
    {
        var config = LoadLockConfig();
        config.Enabled = enabled;
        SaveLockConfig(config);
    }

    /// <summary>Records this device's pinned threads so they can be re-applied on next launch.</summary>
    public void RememberAffinity(DeviceInfo device, IReadOnlyCollection<int> threads)
    {
        var config = LoadLockConfig();
        config.Affinities[device.PnpDeviceId] = threads.ToArray();
        SaveLockConfig(config);
    }

    public void ForgetAffinity(DeviceInfo device)
    {
        var config = LoadLockConfig();
        if (config.Affinities.Remove(device.PnpDeviceId)) SaveLockConfig(config);
    }

    /// <returns>How many devices had their pinned affinity re-applied.</returns>
    public int ReapplyLockedAffinities()
    {
        var config = LoadLockConfig();
        if (!config.Enabled) return 0;

        int count = 0;
        foreach (var (deviceId, threads) in config.Affinities)
        {
            try
            {
                SetInterruptAffinity(new DeviceInfo { PnpDeviceId = deviceId }, threads);
                count++;
            }
            catch { /* device no longer present / access denied - skip it */ }
        }
        return count;
    }

    public static int LogicalProcessorCount => Environment.ProcessorCount;
}
