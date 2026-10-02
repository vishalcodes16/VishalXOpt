# Vishal X Opt

A free, all-in-one Windows optimization & tweaking suite - WPF/.NET 8, MVVM, dark modern UI.
Every tab is free and unlocked; nothing is paywalled.

This repository is a **working build**, not a mockup. Every tab described below - including all
nine Tools-grid entries - is real, functioning code: the registry/service engine, the 41-tweak
catalog, disk cleanup, power-plan management, Autoruns (registry + Startup folder), Debloat, the
WinUtil tab, Devices (MSI Mode) + Interrupts (IRQ affinity, with a "Lock" that survives restarts),
Network adapters (live per-property editing), Tasks, Components, and all nine Tools (GodMode,
Internet test, Bottleneck, StoreX, GameModeX, ProcessX, a scheduler-jitter latency test, GameReadyX,
and Steam library integration).

## Quick start

```bash
git clone https://github.com/<your-username>/VishalXOpt.git
cd VishalXOpt
dotnet restore
dotnet build -c Release
dotnet run --project src/VishalXOpt
```

Run the built `.exe` as **Administrator** - it edits the registry, services and devices, so the
app manifest requests elevation automatically (Windows will prompt with UAC).

**Requirements:** Windows 10 21H2+/11, .NET 8 Desktop Runtime (or the SDK to build from source).

### Build via GitHub Actions

Push to `main` (or open a PR) and `.github/workflows/build.yml` builds the app on a
`windows-latest` runner and uploads a `VishalXOpt-win-x64` artifact you can download from the
Actions tab - no local Windows machine required to get a runnable build. Pushing a tag like
`v1.0.0` additionally zips the publish output and attaches it to a GitHub Release.

## Architecture

Single WPF project (`src/VishalXOpt`), MVVM, no DI container (the app is small enough that
`MainViewModel` just constructs every service once and hands it down - see its constructor).

```
Models/       Plain data classes: TweakDefinition, DeviceInfo, BackupSnapshot, ProcessRule, ...
Mvvm/         ViewModelBase (INotifyPropertyChanged) + RelayCommand/AsyncRelayCommand
Services/     One class per OS mechanism - see the table below
ViewModels/   One per tab/tool, plus the generic TweakTabViewModel / ListTabViewModelBase
              that each drive several tabs at once
Views/        XAML UserControls; App.xaml maps each ViewModel type to its View via
              DataTemplates, so navigation is just "set CurrentContent to a ViewModel"
Themes/       DarkTheme.xaml - dark, card-based look with a fully re-templated (dark) ComboBox
Data/         tweaks.json - the declarative registry/service/process tweak catalog
```

### Two reusable "engines" do most of the work

| Tab(s) | Driven by | Mechanism |
|---|---|---|
| Basic, Security, Customization, Privacy, Tweaks, Deprecated | `TweakTabViewModel` + `Data/tweaks.json` | `RegistryTweakService` (registry DWORD/String writes), `ServiceControlService` (service start-mode via `sc.exe`), or a raw command (e.g. `netsh` for the Firewall toggle) |
| Autoruns, Debloat, Tasks, Components | `ListTabViewModelBase` (4 subclasses) | `AutorunsService` (registry Run-keys *and* the Startup folder), `DebloatService` (PowerShell `Get/Remove-AppxPackage`), `TaskSchedulerService` (`Get-ScheduledTask`/`schtasks.exe`), `DismComponentService` (`Get/Enable/Disable-WindowsOptionalFeature`) |

Everything else (Cleanup, Power management, WinUtil, Devices & Interrupts, Network adapters,
Home, Tools' nine entries, Settings) has its own small, dedicated ViewModel + Service pair - see
`ViewModels/` and `Services/` directly, they're short and meant to be read.

**Every slow call (PowerShell, DISM, WMI, `schtasks`) runs off the UI thread.** List-tab loads,
device/adapter enumeration, Cleanup scans, and the Bottleneck/Internet/Latency tools all use
`AsyncRelayCommand` + `Task.Run`, so opening Debloat or Components never freezes the window.

### The tweak catalog (`Data/tweaks.json`)

41 curated, real tweaks across Basic/Security/Customization/Privacy/Tweaks/Deprecated, each a
JSON object like:

```json
{
  "id": "basic.sysmain",
  "tab": "Basic",
  "name": "SysMain (Prefetch/Superfetch)",
  "risk": "None",
  "kind": "Service",
  "serviceName": "SysMain",
  "serviceDefaultStart": "auto",
  "defaultOn": false, "optimalOn": true, "maximumOn": true
}
```

For `kind: "Service"` tweaks, **"on" means the optimization is applied** (the service is set to
Disabled and stopped); **"off" restores the exact Windows-default start mode** recorded in
`serviceDefaultStart` ("auto", "delayed-auto", or "demand") - not a blind guess, and not just
"Automatic" for services (like Windows Search) whose real default is Delayed Start.

Adding a new tweak is almost always just adding one more JSON object - no C# required unless it
needs a new `kind` (Registry / Service / Process are the three supported today).

**Every tweak in the JSON uses real, documented Windows mechanisms** (MMCSS's
`NetworkThrottlingIndex`/`SystemResponsiveness`, `Win32PrioritySeparation`,
`NtfsDisableLastAccessUpdate`, the DataCollection/Explorer/CloudContent policy keys, the Defender
`Real-Time Protection` policy key, etc.) - sourced from well-known, publicly documented Windows
administration references, not guessed. That said, registry behavior can vary by Windows build;
**test on a VM or a machine with System Restore on before relying on this for a production
fleet.**

### Safety behavior baked into the code

- **Only changed rows are ever written.** Each tweak row tracks whether the person (or a preset)
  actually touched it; Apply only acts on dirty rows, so an unreadable/custom value is never
  silently overwritten just because you clicked Apply on a tab you were only skimming.
- **Risk-gated presets** - any tweak with `"risk"` above `"None"` (Defender, Firewall,
  Smartscreen, UAC, Core Isolation, Spectre/Meltdown mitigations) is *never* touched by a preset
  button, even "Maximum" - `TweakCatalogService.ResolvePresetTarget` returns `null` for those and
  the UI leaves them exactly as they were. The person has to flip that one switch themselves.
- **Backup before Apply, and Undo steps backwards through history** - `TweakTabViewModel.ApplyAsync`
  creates a System Restore point (best-effort) and writes a JSON snapshot of every previous
  registry value *and* service start-mode before changing anything. Settings → **Undo last
  change** replays the most recent snapshot, then retires it, so repeated Undo clicks step back
  through history instead of re-applying the same snapshot forever. Snapshots live in
  `%AppData%\VishalXOpt\Backups\`.
- **Scan-then-confirm cleanup** - Cleanup always sizes a category before deleting it (off the UI
  thread); Event Log clearing defaults to unchecked; the Windows Update service is only
  stopped/restarted if it was actually running before the cache cleanup.
- **No silent network calls** - the WinUtil tab shows the exact command and streams its output
  live rather than running anything invisibly; **Stop actually kills the process tree**, not just
  the output stream.
- **GameModeX and ProcessX clean up after themselves** - closing the window (or the app crashing
  through the global handler) restores every process priority GameModeX touched and switches the
  power plan back to whatever was active before, via `MainWindow.OnClosing` →
  `MainViewModel.Dispose()`.
- **A crash never takes the whole app down** - `App.xaml.cs` catches dispatcher/unhandled
  exceptions, logs them to `%AppData%\VishalXOpt\Logs\crash.log`, and keeps the window open.

## What's implemented

Every tab and every Tools-grid entry is real:

**Optimization tabs:** Basic · Security · Customization · Privacy · Tweaks · Deprecated (all via
the JSON catalog, with correct per-service default start modes) · Cleanup · Power management
(Ultimate Performance with a correctly-captured GUID, CPU min-state, USB suspend) · Autoruns
(registry Run-keys **and** the Startup folder, plus whole-list Backup/Restore) · Debloat (AppX
removal with sizes, loaded async) · WinUtil (live-streamed `christitus.com/win`, Stop kills the
tree) · Devices & Interrupts (MSI Mode + IRQ affinity pinning; **Lock Interrupt Routing persists
and re-applies on every app launch**) · Network adapters (per-property **editable** dropdowns
fed by the driver's own `ValidDisplayValues`, plus a Low Latency preset) · Tasks (curated
built-in scheduled tasks, state read via `Get-ScheduledTask` so it isn't locale-dependent) ·
Components (curated Windows optional features).

**Tools grid (all nine):** GodMode · Internet test · Bottleneck (disposes its GPU counters
properly) · **StoreX** (winget batch-installer over a 15-app curated list) · **GameModeX**
(foreground-process priority boost that follows focus, background-app throttling, Ultimate
Performance for the session - fully reversible) · **ProcessX** (persisted priority rules with a
background re-apply timer) · **PC Latency Test** (a real scheduler-jitter measurement - see the
honesty note below) · **GameReadyX** (pending-reboot, graphics-driver-age, background-app-count
and Windows-Update-activity checks) · **Steam** (reads your local library's VDF files and
launches via `steam://run/`).

**Home dashboard** with a live, computed optimization score. **Settings** with full backup
history and step-backwards Undo.

### One honesty note, kept from the original design

**PC Latency Test** measures *scheduler jitter* (how late a 1ms sleep actually wakes up) - a real,
useful proxy that correlates with the same causes true DPC/ISR latency tools report, but it is
**not** a kernel ETW trace of individual interrupt-service routines the way LatencyMon is. A true
DPC/ISR breakdown needs `Microsoft.Diagnostics.Tracing.TraceEvent` and an NT Kernel Logger
session - a reasonable future upgrade, but a big enough jump that shipping an honestly-labeled
approximation beat shipping an unverified ETW integration. The UI says "scheduler jitter test" for
exactly this reason.

**Lock Interrupt Routing** is an on-launch re-apply (`DeviceInterruptService.ReapplyLockedAffinities`,
called from `MainViewModel`'s constructor), not a background Windows service watching for driver
resets in real time - a true always-on guard would need its own service project, out of scope for
a single desktop app.

## Contributing / extending

- **New simple tweak?** Add one object to `Data/tweaks.json` - done.
- **New tab with its own list of things?** Look at `ListTabViewModelBase` + `ListTabView` first;
  three of the four existing list tabs needed almost no new XAML.
- **New Tools entry?** Follow the StoreX/GameReadyX pattern: one service class, one ViewModel with
  a `BackCommand`, one View registered in `App.xaml`, one card added in `ToolsViewModel`.
- **New OS mechanism?** Add one `Services/XyzService.cs` (see any existing one for the pattern:
  a plain class, no interfaces/DI needed, wrapping either the registry, a `ServiceController`, or
  `ProcessRunner` for a CLI tool).

## License & credits

This repository's own code is MIT-licensed (see `LICENSE`). The WinUtil tab calls Chris Titus
Tech's public script directly from `christitus.com` at runtime - no code from that project is
vendored here. Tab names, toggle concepts and preset patterns were shaped by a feature-parity
review of several existing Windows optimizer UIs (functional labels for standard OS settings, not
creative content) plus general, publicly documented Windows performance-tuning knowledge - no
third-party source code is reproduced in this repository.
