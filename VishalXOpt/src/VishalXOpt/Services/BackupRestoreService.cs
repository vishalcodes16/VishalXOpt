using System.IO;
using System.Management;
using System.Text.Json;
using System.Text.Json.Serialization;
using VishalXOpt.Models;

namespace VishalXOpt.Services;

/// <summary>
/// Every "Apply" flow in the app should: (1) optionally create a System Restore point,
/// (2) record the previous value of anything it's about to change into a <see cref="BackupSnapshot"/>,
/// (3) save that snapshot to disk. <see cref="UndoLast"/> replays the most recent snapshot's
/// previous values back into the registry.
/// </summary>
public sealed class BackupRestoreService
{
    private readonly RegistryTweakService _registry;
    private readonly ServiceControlService _services;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public BackupRestoreService(RegistryTweakService registry, ServiceControlService services)
    {
        _registry = registry;
        _services = services;
    }

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

    /// <summary>Best-effort - many machines have System Restore disabled entirely, which is
    /// fine; the JSON snapshot below is the primary Undo mechanism regardless.</summary>
    public void TryCreateRestorePoint(string description)
    {
        try
        {
            using var systemRestore = new ManagementClass(@"\\.\root\default:SystemRestore");
            using var inParams = systemRestore.GetMethodParameters("CreateRestorePoint");
            inParams["Description"] = description;
            inParams["RestorePointType"] = 12; // MODIFY_SETTINGS
            inParams["EventType"] = 100;        // BEGIN_SYSTEM_CHANGE
            systemRestore.InvokeMethod("CreateRestorePoint", inParams, null);
        }
        catch
        {
            // System Restore may be off, or unavailable on this SKU - not fatal.
        }
    }

    public string Save(BackupSnapshot snapshot)
    {
        var fileName = $"snapshot-{snapshot.TimestampUtc:yyyyMMdd-HHmmss-fff}.json";
        var path = Path.Combine(BackupFolder, fileName);
        File.WriteAllText(path, JsonSerializer.Serialize(snapshot, JsonOptions));
        return path;
    }

    public BackupSnapshot? LoadMostRecent()
    {
        var file = new DirectoryInfo(BackupFolder)
            .GetFiles("snapshot-*.json")
            .OrderByDescending(f => f.LastWriteTimeUtc)
            .FirstOrDefault();

        if (file is null) return null;
        return JsonSerializer.Deserialize<BackupSnapshot>(File.ReadAllText(file.FullName), JsonOptions);
    }

    public List<BackupSnapshot> ListAll() =>
        new DirectoryInfo(BackupFolder).GetFiles("snapshot-*.json")
            .OrderByDescending(f => f.LastWriteTimeUtc)
            .Select(f => JsonSerializer.Deserialize<BackupSnapshot>(File.ReadAllText(f.FullName), JsonOptions))
            .Where(s => s is not null)
            .Select(s => s!)
            .ToList();

    /// <summary>Writes every previous registry value and service start mode in the snapshot
    /// back. Process-kind tweaks (e.g. the Firewall toggle) have no recorded previous state, so
    /// flip those back by hand.</summary>
    public void Undo(BackupSnapshot snapshot)
    {
        // Reverse order so that if two changes touched the same value, the oldest wins.
        foreach (var value in Enumerable.Reverse(snapshot.RegistryValues))
            _registry.Restore(value);

        foreach (var service in snapshot.ServiceValues)
            _services.Restore(service.ServiceName, service.PreviousStartMode);
    }

    /// <summary>Undoes the most recent snapshot, then retires it (renamed to *.undone) so the
    /// next call steps back to the snapshot before it rather than re-applying the same one.</summary>
    public bool UndoLast()
    {
        var file = new DirectoryInfo(BackupFolder)
            .GetFiles("snapshot-*.json")
            .OrderByDescending(f => f.LastWriteTimeUtc)
            .FirstOrDefault();
        if (file is null) return false;

        var snapshot = JsonSerializer.Deserialize<BackupSnapshot>(File.ReadAllText(file.FullName), JsonOptions);
        if (snapshot is null) return false;

        Undo(snapshot);
        File.Move(file.FullName, file.FullName + ".undone", overwrite: true);
        return true;
    }
}
