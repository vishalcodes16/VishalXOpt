using System.IO;
using System.Text.Json;
using Microsoft.Win32;
using VishalXOpt.Models;

namespace VishalXOpt.Services;

/// <summary>
/// Covers both places the large majority of "X starts with Windows" entries actually live:
/// the registry Run keys (HKCU/HKLM \...\CurrentVersion\Run) and the Startup folder (both the
/// current user's and the all-users one). Scheduled-task-based autoruns are intentionally out of
/// scope here since the Tasks tab already covers scheduled tasks directly.
/// </summary>
public sealed class AutorunsService
{
    private static readonly (RegistryHiveKind Hive, string Path)[] RunKeys =
    {
        (RegistryHiveKind.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Run"),
        (RegistryHiveKind.LocalMachine, @"Software\Microsoft\Windows\CurrentVersion\Run"),
        (RegistryHiveKind.LocalMachine, @"Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Run"),
    };

    private const string DisabledSuffix = "-VishalXOptDisabled";
    private const string DisabledFolderName = "VishalXOptDisabled";

    public List<AutorunEntryInfo> Enumerate()
    {
        var entries = new List<AutorunEntryInfo>();

        foreach (var (hive, path) in RunKeys)
        {
            AddRegistryEntriesFrom(entries, hive, path, isEnabled: true);
            AddRegistryEntriesFrom(entries, hive, path + DisabledSuffix, isEnabled: false);
        }

        AddStartupFolderEntries(entries, Environment.GetFolderPath(Environment.SpecialFolder.Startup), isAllUsers: false);
        AddStartupFolderEntries(entries, Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup), isAllUsers: true);

        return entries;
    }

    private static void AddRegistryEntriesFrom(List<AutorunEntryInfo> entries, RegistryHiveKind hive, string path, bool isEnabled)
    {
        try
        {
            using var key = OpenHive(hive).OpenSubKey(path, writable: false);
            if (key is null) return;

            foreach (var name in key.GetValueNames())
            {
                entries.Add(new AutorunEntryInfo
                {
                    Name = name,
                    Command = key.GetValue(name)?.ToString() ?? "",
                    Source = AutorunSourceKind.RegistryRun,
                    Hive = hive,
                    RegistryPath = path,
                    IsEnabled = isEnabled
                });
            }
        }
        catch { /* key missing / inaccessible */ }
    }

    private static void AddStartupFolderEntries(List<AutorunEntryInfo> entries, string folder, bool isAllUsers)
    {
        AddFilesFrom(entries, folder, isAllUsers, isEnabled: true);

        var disabledFolder = Path.Combine(folder, DisabledFolderName);
        AddFilesFrom(entries, disabledFolder, isAllUsers, isEnabled: false);
    }

    private static void AddFilesFrom(List<AutorunEntryInfo> entries, string folder, bool isAllUsers, bool isEnabled)
    {
        if (!Directory.Exists(folder)) return;
        try
        {
            foreach (var file in Directory.EnumerateFiles(folder))
            {
                if (Path.GetFileName(file).Equals("desktop.ini", StringComparison.OrdinalIgnoreCase)) continue;
                entries.Add(new AutorunEntryInfo
                {
                    Name = Path.GetFileNameWithoutExtension(file),
                    Command = file,
                    Source = AutorunSourceKind.StartupFolder,
                    FilePath = file,
                    IsAllUsersStartup = isAllUsers,
                    IsEnabled = isEnabled
                });
            }
        }
        catch { /* access denied on the all-users folder without elevation, etc. */ }
    }

    public void SetEnabled(AutorunEntryInfo entry, bool enable)
    {
        if (entry.Source == AutorunSourceKind.StartupFolder)
        {
            SetFolderEntryEnabled(entry, enable);
            return;
        }

        var sourcePath = enable ? StripDisabledSuffix(entry.RegistryPath) + DisabledSuffix : StripDisabledSuffix(entry.RegistryPath);
        var targetPath = enable ? StripDisabledSuffix(entry.RegistryPath) : StripDisabledSuffix(entry.RegistryPath) + DisabledSuffix;

        using var source = OpenHive(entry.Hive).OpenSubKey(sourcePath, writable: true);
        var value = source?.GetValue(entry.Name)?.ToString();
        if (value is null) return;

        using var target = OpenHive(entry.Hive).CreateSubKey(targetPath, writable: true);
        target?.SetValue(entry.Name, value);
        source?.DeleteValue(entry.Name, throwOnMissingValue: false);
    }

    private static void SetFolderEntryEnabled(AutorunEntryInfo entry, bool enable)
    {
        try
        {
            var baseFolder = entry.IsAllUsersStartup
                ? Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup)
                : Environment.GetFolderPath(Environment.SpecialFolder.Startup);
            var disabledFolder = Path.Combine(baseFolder, DisabledFolderName);
            Directory.CreateDirectory(disabledFolder);

            var fileName = Path.GetFileName(entry.FilePath);
            var target = enable
                ? Path.Combine(baseFolder, fileName)
                : Path.Combine(disabledFolder, fileName);

            if (File.Exists(entry.FilePath) && !string.Equals(entry.FilePath, target, StringComparison.OrdinalIgnoreCase))
                File.Move(entry.FilePath, target, overwrite: true);
        }
        catch { /* file locked / access denied - best effort */ }
    }

    public void Delete(AutorunEntryInfo entry)
    {
        if (entry.Source == AutorunSourceKind.StartupFolder)
        {
            try { if (File.Exists(entry.FilePath)) File.Delete(entry.FilePath); } catch { /* best effort */ }
            return;
        }

        using var key = OpenHive(entry.Hive).OpenSubKey(entry.RegistryPath, writable: true);
        key?.DeleteValue(entry.Name, throwOnMissingValue: false);
    }

    private static string StripDisabledSuffix(string path) =>
        path.EndsWith(DisabledSuffix, StringComparison.Ordinal) ? path[..^DisabledSuffix.Length] : path;

    private static string BackupFolder
    {
        get
        {
            var folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "VishalXOpt", "Backups");
            Directory.CreateDirectory(folder);
            return folder;
        }
    }

    /// <summary>Snapshots every currently-enabled autostart entry (registry and Startup-folder)
    /// to a JSON file, so the whole startup list can be restored in one click later.</summary>
    public string BackupToFile()
    {
        var entries = Enumerate().Where(e => e.IsEnabled).ToList();
        var path = Path.Combine(BackupFolder, $"startup-backup-{DateTime.Now:yyyyMMdd-HHmmss}.json");
        File.WriteAllText(path, JsonSerializer.Serialize(entries, new JsonSerializerOptions { WriteIndented = true }));
        return path;
    }

    public string? FindLatestBackup() =>
        new DirectoryInfo(BackupFolder).GetFiles("startup-backup-*.json")
            .OrderByDescending(f => f.LastWriteTimeUtc)
            .FirstOrDefault()?.FullName;

    /// <summary>Re-creates any missing <em>registry-based</em> entry from the backup. Startup-
    /// folder entries are recorded in the backup for visibility but can't be recreated from it -
    /// there's no way to reconstruct a deleted .lnk/.exe's actual bytes from a JSON pointer to
    /// where it used to live, so those are reported separately rather than silently skipped.</summary>
    public (int Restored, int SkippedFolderEntries) RestoreFromFile(string path)
    {
        var saved = JsonSerializer.Deserialize<List<AutorunEntryInfo>>(File.ReadAllText(path)) ?? new();
        var current = Enumerate().ToList();
        var currentRegistryKeys = current
            .Where(e => e.Source == AutorunSourceKind.RegistryRun)
            .Select(e => (e.Hive, e.RegistryPath, e.Name))
            .ToHashSet();
        var currentFolderNames = current
            .Where(e => e.Source == AutorunSourceKind.StartupFolder)
            .Select(e => e.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        int restored = 0, skipped = 0;
        foreach (var entry in saved)
        {
            if (entry.Source == AutorunSourceKind.StartupFolder)
            {
                if (!currentFolderNames.Contains(entry.Name)) skipped++;
                continue;
            }

            if (currentRegistryKeys.Contains((entry.Hive, entry.RegistryPath, entry.Name))) continue;
            using var key = OpenHive(entry.Hive).CreateSubKey(entry.RegistryPath, writable: true);
            key?.SetValue(entry.Name, entry.Command);
            restored++;
        }
        return (restored, skipped);
    }

    private static RegistryKey OpenHive(RegistryHiveKind hive) =>
        hive == RegistryHiveKind.LocalMachine ? Registry.LocalMachine : Registry.CurrentUser;
}
