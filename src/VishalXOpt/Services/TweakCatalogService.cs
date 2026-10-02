using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using VishalXOpt.Models;

namespace VishalXOpt.Services;

/// <summary>Loads the declarative tweak list from <c>Data/tweaks.json</c> and answers
/// "what should tab X look like under preset Y" queries.</summary>
public sealed class TweakCatalogService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private List<TweakDefinition> _all = new();

    public IReadOnlyList<TweakDefinition> All => _all;

    public void Load()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Data", "tweaks.json");
        if (!File.Exists(path))
        {
            _all = new List<TweakDefinition>();
            return;
        }

        var json = File.ReadAllText(path);
        var catalog = JsonSerializer.Deserialize<TweakCatalog>(json, JsonOptions) ?? new TweakCatalog();
        _all = catalog.Tweaks;
    }

    public IEnumerable<TweakDefinition> ForTab(string tab) =>
        _all.Where(t => string.Equals(t.Tab, tab, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// What state a preset wants for this tweak. Risk &gt; None tweaks (Defender, Firewall,
    /// Smartscreen, Core Isolation, CPU mitigations, ...) are deliberately excluded from every
    /// preset - "Maximum" never silently weakens a security control. They only change when the
    /// person explicitly flips that one switch themselves. See README "Safety Design".
    /// </summary>
    public bool? ResolvePresetTarget(TweakDefinition tweak, PresetLevel preset)
    {
        if (tweak.Risk != RiskLevel.None) return null; // leave as-is, no matter the preset

        return preset switch
        {
            PresetLevel.Default => tweak.DefaultOn,
            PresetLevel.Optimal => tweak.OptimalOn,
            PresetLevel.Maximum => tweak.MaximumOn,
            _ => null
        };
    }
}
