namespace VishalXOpt.Services;

public sealed record WingetAppInfo(string Name, string WingetId, string Category);

/// <summary>Backs the StoreX tool: a curated list of everyday apps, batch-installed via
/// <c>winget</c> (present by default on Windows 10 1809+/11 as "App Installer").</summary>
public sealed class StoreXService
{
    public static readonly IReadOnlyList<WingetAppInfo> CuratedApps = new List<WingetAppInfo>
    {
        new("Google Chrome", "Google.Chrome", "Browsers"),
        new("Mozilla Firefox", "Mozilla.Firefox", "Browsers"),
        new("7-Zip", "7zip.7zip", "Utilities"),
        new("VLC media player", "VideoLAN.VLC", "Media"),
        new("Notepad++", "Notepad++.Notepad++", "Utilities"),
        new("Discord", "Discord.Discord", "Communication"),
        new("Steam", "Valve.Steam", "Gaming"),
        new("Visual Studio Code", "Microsoft.VisualStudioCode", "Development"),
        new("PowerToys", "Microsoft.PowerToys", "Utilities"),
        new("Spotify", "Spotify.Spotify", "Media"),
        new("Zoom", "Zoom.Zoom", "Communication"),
        new("WinRAR", "RARLab.WinRAR", "Utilities"),
        new("Git", "Git.Git", "Development"),
        new("qBittorrent", "qBittorrent.qBittorrent", "Utilities"),
        new("MSI Afterburner", "Guru3D.Afterburner", "Gaming"),
    };

    public bool IsWingetAvailable() => ProcessRunner.RunAndWait("winget.exe", "--version").ExitCode == 0;

    /// <returns>true if the install command completed with exit code 0.</returns>
    public bool Install(WingetAppInfo app, int timeoutMs = 300_000)
    {
        var result = ProcessRunner.RunAndWait(
            "winget.exe",
            $"install --id {app.WingetId} -e --silent --accept-package-agreements --accept-source-agreements",
            timeoutMs);
        return result.ExitCode == 0;
    }
}
