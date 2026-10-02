namespace VishalXOpt.Models;

/// <summary>How a <see cref="TweakDefinition"/> is actually applied to the system.</summary>
public enum TweakKind
{
    /// <summary>One or more registry values are written.</summary>
    Registry,

    /// <summary>A Windows service's StartType (and running state) is changed.</summary>
    Service,

    /// <summary>An external command (netsh, dism, schtasks, ...) is run.</summary>
    Process
}

/// <summary>How risky a tweak is considered. Anything above <see cref="None"/> is shown
/// with a warning badge in the UI and is never silently applied by a preset.</summary>
public enum RiskLevel
{
    None,
    Low,
    Medium,
    High
}

/// <summary>The built-in quick presets shown at the top of every Optimization tab.</summary>
public enum PresetLevel
{
    Default,
    Optimal,
    Maximum
}

/// <summary>Which registry hive a <see cref="RegistryEntry"/> lives under.</summary>
public enum RegistryHiveKind
{
    LocalMachine,
    CurrentUser
}

/// <summary>The registry value type to write. Deliberately named differently from
/// <see cref="Microsoft.Win32.RegistryValueKind"/> so the two never collide as an ambiguous
/// unqualified reference in a file that has both namespaces in scope.</summary>
public enum TweakValueKind
{
    DWord,
    String,
    Binary
}
