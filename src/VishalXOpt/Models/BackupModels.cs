namespace VishalXOpt.Models;

/// <summary>The previous state of one changed registry value, recorded before a tweak is applied.</summary>
public sealed class BackupRegistryValue
{
    public RegistryHiveKind Hive { get; set; }
    public string Path { get; set; } = "";
    public string ValueName { get; set; } = "";
    public string? PreviousValue { get; set; } // null = the value did not exist before
    public TweakValueKind ValueKind { get; set; }
}

/// <summary>The previous start mode of a service changed by a tweak ("auto", "delayed-auto",
/// "demand" or "disabled" - the values <c>sc config start=</c> accepts).</summary>
public sealed class BackupServiceValue
{
    public string ServiceName { get; set; } = "";
    public string PreviousStartMode { get; set; } = "auto";
}

/// <summary>One full "Apply" run, saved to disk so it can be undone later.</summary>
public sealed class BackupSnapshot
{
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
    public string Description { get; set; } = "";
    public List<BackupRegistryValue> RegistryValues { get; set; } = new();
    public List<BackupServiceValue> ServiceValues { get; set; } = new();
    public List<string> ChangedTweakIds { get; set; } = new();
}
