namespace VishalXOpt.Models;

/// <summary>
/// One registry value that must be written for a <see cref="TweakDefinition"/> of kind
/// <see cref="TweakKind.Registry"/>. A tweak can carry more than one of these (e.g. mouse
/// acceleration needs three values written together).
/// </summary>
public sealed class RegistryEntry
{
    public RegistryHiveKind Hive { get; set; } = RegistryHiveKind.CurrentUser;
    public string Path { get; set; } = "";
    public string ValueName { get; set; } = "";
    public TweakValueKind ValueKind { get; set; } = TweakValueKind.DWord;

    /// <summary>Value written when the tweak is switched ON.</summary>
    public string OnValue { get; set; } = "1";

    /// <summary>Value written when the tweak is switched OFF (restores the Windows default).</summary>
    public string OffValue { get; set; } = "0";
}

/// <summary>
/// A single, declarative tweak shown as one row/card in a <c>TweakTabView</c>.
/// The same class drives the Basic, Security, Customization, Privacy, Tweaks and
/// Deprecated tabs described in the project README — only the <see cref="Tab"/> value
/// and the underlying <see cref="Kind"/> differ.
/// </summary>
public sealed class TweakDefinition
{
    public string Id { get; set; } = "";
    public string Tab { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";

    /// <summary>Optional "Will not work if disabled: ..." style dependency note.</summary>
    public string? DependencyWarning { get; set; }

    public RiskLevel Risk { get; set; } = RiskLevel.None;

    public TweakKind Kind { get; set; } = TweakKind.Registry;

    // --- Kind == Registry -------------------------------------------------
    public List<RegistryEntry> RegistryEntries { get; set; } = new();

    // --- Kind == Service ----------------------------------------------------
    /// <summary>Windows service name (e.g. "SysMain") when <see cref="Kind"/> is Service.</summary>
    public string? ServiceName { get; set; }

    /// <summary>The Windows-default start mode this service returns to when the tweak is switched
    /// OFF: one of "auto", "delayed-auto" or "demand" (the values <c>sc config start=</c> accepts).</summary>
    public string ServiceDefaultStart { get; set; } = "auto";

    // --- Kind == Process ------------------------------------------------
    /// <summary>Command line run when the tweak is switched ON (Kind == Process).</summary>
    public string? OnCommand { get; set; }

    /// <summary>Command line run when the tweak is switched OFF (Kind == Process).</summary>
    public string? OffCommand { get; set; }

    // --- Presets --------------------------------------------------------
    /// <summary>Whether each quick preset wants this tweak switched on.
    /// Risk &gt; None tweaks are never turned on automatically by a preset — see
    /// <c>TweakCatalogService.ResolvePresetState</c>.</summary>
    public bool DefaultOn { get; set; }
    public bool OptimalOn { get; set; }
    public bool MaximumOn { get; set; }
}

/// <summary>Root object deserialized from <c>Data/tweaks.json</c>.</summary>
public sealed class TweakCatalog
{
    public List<TweakDefinition> Tweaks { get; set; } = new();
}
