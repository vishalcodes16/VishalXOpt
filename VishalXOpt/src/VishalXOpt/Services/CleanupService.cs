using System.IO;

namespace VishalXOpt.Services;

public sealed record CleanupCategory(string Id, string Name, string Description, bool IsOptIn);

/// <summary>Backs the Cleanup tab: always scan-and-size before deleting, per the project's
/// Safety Design guidelines.</summary>
public sealed class CleanupService
{
    public static readonly IReadOnlyList<CleanupCategory> Categories = new List<CleanupCategory>
    {
        new("temp", "Temp folders", "%TEMP% and C:\\Windows\\Temp", IsOptIn: false),
        new("prefetch", "Prefetch cache", "C:\\Windows\\Prefetch", IsOptIn: false),
        new("recyclebin", "Recycle Bin", "Everything currently in the Recycle Bin", IsOptIn: false),
        new("thumbnails", "Thumbnail cache", "Explorer's cached thumbnail images", IsOptIn: false),
        new("winupdate", "Windows Update cache", "C:\\Windows\\SoftwareDistribution\\Download", IsOptIn: false),
        new("eventlogs", "Event Logs", "Clears every Windows Event Log - removes diagnostic history", IsOptIn: true),
    };

    public long ScanCategory(string categoryId)
    {
        try
        {
            return categoryId switch
            {
                "temp" => DirSize(Path.GetTempPath()) + DirSize(@"C:\Windows\Temp"),
                "prefetch" => DirSize(@"C:\Windows\Prefetch"),
                "recyclebin" => 0, // size isn't scanned (needs the shell API) - it is still emptied on Clean
                "thumbnails" => FilesSize(ThumbnailFolder, "thumbcache_*.db"),
                "winupdate" => DirSize(@"C:\Windows\SoftwareDistribution\Download"),
                "eventlogs" => 0,
                _ => 0
            };
        }
        catch
        {
            return 0;
        }
    }

    public void Clean(string categoryId)
    {
        switch (categoryId)
        {
            case "temp":
                DeleteContents(Path.GetTempPath());
                DeleteContents(@"C:\Windows\Temp");
                break;
            case "prefetch":
                DeleteContents(@"C:\Windows\Prefetch");
                break;
            case "recyclebin":
                ProcessRunner.RunPowerShell("Clear-RecycleBin -Force -ErrorAction SilentlyContinue");
                break;
            case "thumbnails":
                DeleteContents(ThumbnailFolder, pattern: "thumbcache_*.db", includeSubfolders: false);
                break;
            case "winupdate":
                bool wasRunning = IsServiceRunning("wuauserv");
                if (wasRunning) ProcessRunner.RunAndWait("net.exe", "stop wuauserv");
                DeleteContents(@"C:\Windows\SoftwareDistribution\Download");
                if (wasRunning) ProcessRunner.RunAndWait("net.exe", "start wuauserv");
                break;
            case "eventlogs":
                // Opt-in only - never called from a preset, only an explicit user click.
                var logs = ProcessRunner.RunAndWait("wevtutil.exe", "el");
                foreach (var log in logs.StandardOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries))
                    ProcessRunner.RunAndWait("wevtutil.exe", $"cl \"{log.Trim()}\"");
                break;
        }
    }

    private static long DirSize(string path)
    {
        if (!Directory.Exists(path)) return 0;
        long total = 0;
        try
        {
            foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
            {
                try { total += new FileInfo(file).Length; } catch { /* locked/in-use file */ }
            }
        }
        catch { /* access denied on a subfolder */ }
        return total;
    }

    private static string ThumbnailFolder => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Microsoft", "Windows", "Explorer");

    private static bool IsServiceRunning(string name)
    {
        try
        {
            using var sc = new System.ServiceProcess.ServiceController(name);
            return sc.Status == System.ServiceProcess.ServiceControllerStatus.Running;
        }
        catch { return false; }
    }

    private static long FilesSize(string folder, string pattern)
    {
        long total = 0;
        foreach (var file in SafeEnumerate(folder, pattern))
        {
            try { total += new FileInfo(file).Length; } catch { /* locked */ }
        }
        return total;
    }

    private static void DeleteContents(string path, string pattern = "*", bool includeSubfolders = true)
    {
        if (!Directory.Exists(path)) return;
        foreach (var file in SafeEnumerate(path, pattern))
        {
            try { File.Delete(file); } catch { /* in use - skip */ }
        }
        if (!includeSubfolders) return;
        foreach (var dir in SafeEnumerateDirs(path))
        {
            try { Directory.Delete(dir, recursive: true); } catch { /* in use - skip */ }
        }
    }

    private static IEnumerable<string> SafeEnumerate(string path, string pattern)
    {
        try { return Directory.EnumerateFiles(path, pattern, SearchOption.TopDirectoryOnly); }
        catch { return Enumerable.Empty<string>(); }
    }

    private static IEnumerable<string> SafeEnumerateDirs(string path)
    {
        try { return Directory.EnumerateDirectories(path); }
        catch { return Enumerable.Empty<string>(); }
    }
}
