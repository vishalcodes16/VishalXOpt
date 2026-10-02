using System.Diagnostics;

namespace VishalXOpt.Models;

/// <summary>One user-defined ProcessX rule: "whenever a process named X is running, set its
/// priority to Y". Matching is by process image name (without .exe), case-insensitive.</summary>
public sealed class ProcessRule
{
    public string ProcessName { get; set; } = "";
    public ProcessPriorityClass Priority { get; set; } = ProcessPriorityClass.Normal;
    public bool Enabled { get; set; } = true;
}
