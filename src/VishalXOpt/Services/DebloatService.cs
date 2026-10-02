using VishalXOpt.Models;

namespace VishalXOpt.Services;

/// <summary>Backs the Debloat tab's "UWP Applications" list.</summary>
public sealed class DebloatService
{
    public List<AppxPackageInfo> ListRemovableApps()
    {
        // -Name / -PackageFullName / an approximate size via the install location.
        const string script =
            "Get-AppxPackage | Where-Object { -not $_.IsFramework -and -not $_.NonRemovable } | " +
            "ForEach-Object { $size = 0; try { $size = (Get-ChildItem $_.InstallLocation -Recurse -ErrorAction SilentlyContinue | " +
            "Measure-Object -Property Length -Sum).Sum } catch {}; " +
            "\"$($_.Name)|$($_.PackageFullName)|$size\" }";

        var result = ProcessRunner.RunPowerShell(script, timeoutMs: 60_000);
        var list = new List<AppxPackageInfo>();

        foreach (var line in result.StandardOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = line.Trim().Split('|');
            if (parts.Length != 3) continue;
            _ = double.TryParse(parts[2], out var bytes);

            list.Add(new AppxPackageInfo
            {
                Name = parts[0],
                PackageFullName = parts[1],
                SizeMb = Math.Round(bytes / 1024d / 1024d, 2)
            });
        }

        return list;
    }

    public void Remove(AppxPackageInfo app)
    {
        ProcessRunner.RunPowerShell($"Remove-AppxPackage -Package '{app.PackageFullName}' -ErrorAction SilentlyContinue");
        // Also stop it reinstalling for new user profiles on this machine.
        var provisionedName = app.PackageFullName.Split('_')[0];
        ProcessRunner.RunPowerShell(
            $"Get-AppxProvisionedPackage -Online | Where-Object DisplayName -eq '{provisionedName}' | " +
            "Remove-AppxProvisionedPackage -Online -ErrorAction SilentlyContinue");
    }
}
