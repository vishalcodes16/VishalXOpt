using Microsoft.Win32;
using VishalXOpt.Models;

namespace VishalXOpt.Services;

/// <summary>
/// Reads and writes the registry values behind every <see cref="TweakDefinition"/> of kind
/// <see cref="TweakKind.Registry"/>. This is the single most-reused piece of the app: it is
/// what actually drives the Basic, Security, Customization, Privacy, Tweaks and Deprecated
/// tabs, all of which are just different slices of the same declarative tweak list.
/// </summary>
public sealed class RegistryTweakService
{
    private static RegistryKey OpenHive(RegistryHiveKind hive) =>
        hive == RegistryHiveKind.LocalMachine ? Registry.LocalMachine : Registry.CurrentUser;

    /// <summary>Returns true if every registry entry currently matches "on", false if every
    /// entry matches "off", or null if the entries are missing/mixed (unknown state).</summary>
    public bool? ReadIsOn(TweakDefinition tweak)
    {
        if (tweak.Kind != TweakKind.Registry || tweak.RegistryEntries.Count == 0) return null;

        bool? result = null;
        foreach (var entry in tweak.RegistryEntries)
        {
            bool? entryState = TryGetEntryState(entry);
            if (entryState is null) return null;             // unknown / custom value -> unknown overall
            if (result is null) result = entryState;
            else if (result != entryState) return null;       // entries disagree -> unknown overall
        }
        return result;
    }

    private bool? TryGetEntryState(RegistryEntry entry)
    {
        var current = ReadValue(entry);
        if (current is null) return null;

        if (entry.ValueKind == TweakValueKind.DWord)
        {
            if (!TryParseDWord(current, out var currentInt)) return null;
            if (TryParseDWord(entry.OnValue, out var onInt) && currentInt == onInt) return true;
            if (TryParseDWord(entry.OffValue, out var offInt) && currentInt == offInt) return false;
            return null;
        }

        if (string.Equals(current, entry.OnValue, StringComparison.OrdinalIgnoreCase)) return true;
        if (string.Equals(current, entry.OffValue, StringComparison.OrdinalIgnoreCase)) return false;
        return null;
    }

    public string? ReadValue(RegistryEntry entry)
    {
        try
        {
            using var key = OpenHive(entry.Hive).OpenSubKey(entry.Path, writable: false);
            var raw = key?.GetValue(entry.ValueName);
            return raw switch
            {
                null => null,
                byte[] bytes => Convert.ToHexString(bytes),
                _ => raw.ToString()
            };
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Applies the given on/off state, returning the previous values so the caller
    /// can add them to a <see cref="BackupSnapshot"/> for Undo.</summary>
    public List<BackupRegistryValue> Apply(TweakDefinition tweak, bool turnOn)
    {
        var previous = new List<BackupRegistryValue>();
        if (tweak.Kind != TweakKind.Registry) return previous;

        foreach (var entry in tweak.RegistryEntries)
        {
            previous.Add(new BackupRegistryValue
            {
                Hive = entry.Hive,
                Path = entry.Path,
                ValueName = entry.ValueName,
                PreviousValue = ReadValue(entry),
                ValueKind = entry.ValueKind
            });

            WriteValue(entry, turnOn ? entry.OnValue : entry.OffValue);
        }
        return previous;
    }

    /// <summary>Writes back a value captured in a backup snapshot (used by Undo).</summary>
    public void Restore(BackupRegistryValue value)
    {
        var entry = new RegistryEntry
        {
            Hive = value.Hive,
            Path = value.Path,
            ValueName = value.ValueName,
            ValueKind = value.ValueKind
        };

        if (value.PreviousValue is null)
        {
            try
            {
                using var key = OpenHive(value.Hive).OpenSubKey(value.Path, writable: true);
                key?.DeleteValue(value.ValueName, throwOnMissingValue: false);
            }
            catch { /* best-effort restore */ }
            return;
        }

        WriteValue(entry, value.PreviousValue);
    }

    private void WriteValue(RegistryEntry entry, string value)
    {
        using var key = OpenHive(entry.Hive).CreateSubKey(entry.Path, writable: true)
            ?? throw new InvalidOperationException($"Could not open/create {entry.Path}");

        switch (entry.ValueKind)
        {
            case TweakValueKind.DWord:
                key.SetValue(entry.ValueName, ParseDWord(value), Microsoft.Win32.RegistryValueKind.DWord);
                break;
            case TweakValueKind.Binary:
                key.SetValue(entry.ValueName, Convert.FromHexString(value), Microsoft.Win32.RegistryValueKind.Binary);
                break;
            default:
                key.SetValue(entry.ValueName, value, Microsoft.Win32.RegistryValueKind.String);
                break;
        }
    }

    // Allows tweak JSON to express DWORD values either as plain decimal ("0", "38") or as
    // 0x-prefixed hex ("0x26", "0xffffffff"). Large hex values (e.g. 0xffffffff) are parsed
    // through Convert.ToInt32's two's-complement round trip so the correct 32-bit pattern
    // still lands in the registry even though it doesn't fit as a positive Int32.
    private static int ParseDWord(string value)
    {
        if (!TryParseDWord(value, out var result))
            throw new FormatException($"'{value}' is not a valid DWORD (decimal or 0x-hex).");
        return result;
    }

    private static bool TryParseDWord(string value, out int result)
    {
        try
        {
            result = value.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                ? Convert.ToInt32(value[2..], 16)
                : Convert.ToInt32(value, 10);
            return true;
        }
        catch
        {
            result = 0;
            return false;
        }
    }
}
