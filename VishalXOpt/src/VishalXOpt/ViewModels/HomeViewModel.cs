using VishalXOpt.Models;
using VishalXOpt.Mvvm;
using VishalXOpt.Services;

namespace VishalXOpt.ViewModels;

public sealed class HomeViewModel : ViewModelBase
{
    private static readonly string[] Tips =
    {
        "Laptops: only enable the Ultimate Performance power plan while plugged in - it disables most battery-saving behavior.",
        "Before trying the Deprecated tab's expert tweaks, make sure Windows System Restore is turned on.",
        "Add your game/build folders as a Defender exclusion instead of disabling real-time protection outright - you get most of the performance back with none of the risk.",
        "The Autoruns tab disables entries reversibly by default - use Delete only once you're sure you don't need that autostart entry back.",
        "MSI Mode and Interrupt Affinity changes are per-device - if one causes an issue, you can revert just that device from the Devices & Interrupts tab."
    };

    public string Greeting => $"Hello, {Environment.UserName}!";
    public string VersionText => "Vishal X Opt - v1.0.0";
    public string Tip { get; } = Tips[Random.Shared.Next(Tips.Length)];

    private int _optimizationScore;
    public int OptimizationScore
    {
        get => _optimizationScore;
        private set => SetField(ref _optimizationScore, value);
    }

    public string OptimizationLevel => OptimizationScore switch
    {
        >= 80 => "Excellent",
        >= 50 => "Good",
        >= 20 => "Getting there",
        _ => "Untouched"
    };

    public RelayCommand StartOptimizationCommand { get; }
    public RelayCommand OpenToolsCommand { get; }

    public HomeViewModel(TweakCatalogService catalog, RegistryTweakService registry, Action goToOptimization, Action goToTools)
    {
        StartOptimizationCommand = new RelayCommand(_ => goToOptimization());
        OpenToolsCommand = new RelayCommand(_ => goToTools());
        OptimizationScore = ComputeScore(catalog, registry);
    }

    /// <summary>A lightweight, illustrative score: of every registry-kind, non-risky tweak in
    /// the catalog, what % currently matches its "Optimal" target. Service/Process-kind tweaks
    /// are skipped here to keep the dashboard snappy (no shelling out just to draw a number).</summary>
    private static int ComputeScore(TweakCatalogService catalog, RegistryTweakService registry)
    {
        var relevant = catalog.All.Where(t => t.Kind == TweakKind.Registry && t.Risk == RiskLevel.None).ToList();
        if (relevant.Count == 0) return 0;

        int matching = 0;
        foreach (var tweak in relevant)
        {
            var current = registry.ReadIsOn(tweak);
            if (current == tweak.OptimalOn) matching++;
        }
        return (int)Math.Round(matching * 100.0 / relevant.Count);
    }
}
