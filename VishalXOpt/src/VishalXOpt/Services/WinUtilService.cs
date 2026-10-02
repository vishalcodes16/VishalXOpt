namespace VishalXOpt.Services;

/// <summary>
/// Backs the WinUtil tab. This intentionally does NOT run silently - the person sees the
/// exact command before it runs and watches its output live, per the project's Safety Design
/// ("no silent network calls").
/// </summary>
public sealed class WinUtilService
{
    public const string Command = "irm https://christitus.com/win | iex";

    public Task<int> RunAsync(Action<string> onLine, CancellationToken ct = default) =>
        ProcessRunner.RunStreamingAsync(
            "powershell.exe",
            $"-NoProfile -ExecutionPolicy Bypass -Command \"{Command}\"",
            onLine,
            ct);
}
