using System.IO;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace VishalXOpt.Services;

public sealed record SteamGameInfo(string Name, string AppId);

/// <summary>
/// Reads Steam's own plain-text library/manifest files (Valve's documented VDF format) - no
/// Steam API key or running client required. Regex-based parsing is enough for the two fields
/// we need (path / appid+name); a full VDF grammar isn't necessary here.
/// </summary>
public sealed class SteamService
{
    private static readonly Regex PathRegex = new("\"path\"\\s*\"([^\"]+)\"", RegexOptions.IgnoreCase);
    private static readonly Regex AppIdRegex = new("\"appid\"\\s*\"(\\d+)\"", RegexOptions.IgnoreCase);
    private static readonly Regex NameRegex = new("\"name\"\\s*\"([^\"]+)\"", RegexOptions.IgnoreCase);

    public string? FindSteamPath()
    {
        try
        {
            using var hkcu = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam");
            var path = hkcu?.GetValue("SteamPath")?.ToString();
            if (!string.IsNullOrWhiteSpace(path)) return path.Replace('/', Path.DirectorySeparatorChar);

            using var hklm = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\Valve\Steam")
                             ?? Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Valve\Steam");
            var installPath = hklm?.GetValue("InstallPath")?.ToString();
            return installPath;
        }
        catch
        {
            return null;
        }
    }

    public List<SteamGameInfo> ListInstalledGames()
    {
        var games = new List<SteamGameInfo>();
        var steamPath = FindSteamPath();
        if (steamPath is null || !Directory.Exists(steamPath)) return games;

        foreach (var libraryPath in ListLibraryFolders(steamPath))
        {
            var steamAppsFolder = Path.Combine(libraryPath, "steamapps");
            if (!Directory.Exists(steamAppsFolder)) continue;

            foreach (var manifest in SafeEnumerateFiles(steamAppsFolder, "appmanifest_*.acf"))
            {
                try
                {
                    var text = File.ReadAllText(manifest);
                    var appIdMatch = AppIdRegex.Match(text);
                    var nameMatch = NameRegex.Match(text);
                    if (appIdMatch.Success && nameMatch.Success)
                        games.Add(new SteamGameInfo(nameMatch.Groups[1].Value, appIdMatch.Groups[1].Value));
                }
                catch { /* unreadable/locked manifest - skip */ }
            }
        }

        return games.DistinctBy(g => g.AppId).OrderBy(g => g.Name).ToList();
    }

    private List<string> ListLibraryFolders(string steamPath)
    {
        var libraries = new List<string> { steamPath };
        var vdfPath = Path.Combine(steamPath, "steamapps", "libraryfolders.vdf");
        if (!File.Exists(vdfPath)) return libraries;

        try
        {
            var text = File.ReadAllText(vdfPath);
            foreach (Match match in PathRegex.Matches(text))
            {
                var path = match.Groups[1].Value.Replace("\\\\", "\\");
                if (Directory.Exists(path) && !libraries.Contains(path, StringComparer.OrdinalIgnoreCase))
                    libraries.Add(path);
            }
        }
        catch { /* fall back to just the main Steam folder */ }

        return libraries;
    }

    private static IEnumerable<string> SafeEnumerateFiles(string folder, string pattern)
    {
        try { return Directory.EnumerateFiles(folder, pattern); }
        catch { return Enumerable.Empty<string>(); }
    }

    /// <summary>Launches a game through Steam's own documented URI protocol - Steam itself
    /// handles starting up if it isn't already running.</summary>
    public void LaunchGame(string appId)
    {
        var psi = new System.Diagnostics.ProcessStartInfo("steam://run/" + appId) { UseShellExecute = true };
        System.Diagnostics.Process.Start(psi);
    }
}
