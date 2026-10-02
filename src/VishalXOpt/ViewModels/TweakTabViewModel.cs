using System.Collections.ObjectModel;
using VishalXOpt.Models;
using VishalXOpt.Mvvm;
using VishalXOpt.Services;

namespace VishalXOpt.ViewModels;

/// <summary>
/// Backs every registry/service/process-driven tab (Basic, Security, Customization, Privacy,
/// Tweaks, Deprecated). Which tab it is comes entirely from the <c>tabName</c> passed to the
/// constructor, which filters <see cref="TweakCatalogService.ForTab"/>.
/// </summary>
public sealed class TweakTabViewModel : ViewModelBase
{
    private readonly TweakCatalogService _catalog;
    private readonly RegistryTweakService _registry;
    private readonly ServiceControlService _serviceControl;
    private readonly BackupRestoreService _backup;
    private readonly string _tabName;

    public string TabName { get; }
    public ObservableCollection<TweakItemViewModel> Items { get; } = new();

    private string _statusMessage = "";
    public string StatusMessage
    {
        get => _statusMessage;
        set => SetField(ref _statusMessage, value);
    }

    private bool _isBusy;
    public bool IsBusy
    {
        get => _isBusy;
        set => SetField(ref _isBusy, value);
    }

    public AsyncRelayCommand ApplyCommand { get; }
    public RelayCommand ApplyDefaultCommand { get; }
    public RelayCommand ApplyOptimalCommand { get; }
    public RelayCommand ApplyMaximumCommand { get; }
    public RelayCommand RefreshCommand { get; }

    public TweakTabViewModel(
        string tabName,
        TweakCatalogService catalog,
        RegistryTweakService registry,
        ServiceControlService serviceControl,
        BackupRestoreService backup)
    {
        _tabName = tabName;
        TabName = tabName;
        _catalog = catalog;
        _registry = registry;
        _serviceControl = serviceControl;
        _backup = backup;

        ApplyCommand = new AsyncRelayCommand(ApplyAsync);
        ApplyDefaultCommand = new RelayCommand(() => ApplyPreset(PresetLevel.Default));
        ApplyOptimalCommand = new RelayCommand(() => ApplyPreset(PresetLevel.Optimal));
        ApplyMaximumCommand = new RelayCommand(() => ApplyPreset(PresetLevel.Maximum));
        RefreshCommand = new RelayCommand(LoadItems);

        LoadItems();
    }

    private void LoadItems()
    {
        Items.Clear();
        foreach (var tweak in _catalog.ForTab(_tabName))
        {
            bool? current = tweak.Kind switch
            {
                TweakKind.Registry => _registry.ReadIsOn(tweak),
                TweakKind.Service => tweak.ServiceName is null ? null : _serviceControl.ReadIsApplied(tweak.ServiceName),
                _ => null // Process-kind tweaks (e.g. the Firewall toggle) have no reliable read-back
            };
            Items.Add(new TweakItemViewModel(tweak, current));
        }
        StatusMessage = $"{Items.Count} settings loaded.";
    }

    private void ApplyPreset(PresetLevel preset)
    {
        foreach (var item in Items)
        {
            var target = _catalog.ResolvePresetTarget(item.Definition, preset);
            if (target is not null) item.IsOn = target.Value; // risky tweaks return null and stay untouched
        }
        var pending = Items.Count(i => i.IsDirty);
        StatusMessage = $"\"{preset}\" preset selected - {pending} change(s) pending. Click Apply to save them.";
    }

    private async Task ApplyAsync()
    {
        var work = Items.Where(i => i.IsDirty).Select(i => (i.Definition, i.IsOn)).ToList();
        if (work.Count == 0)
        {
            StatusMessage = "Nothing to apply - no settings were changed.";
            return;
        }

        IsBusy = true;
        StatusMessage = $"Applying {work.Count} change(s)...";
        try
        {
            // Registry writes, sc.exe calls and the restore point are all blocking, so they run
            // on a background thread; only the view-model updates below touch the UI thread.
            var snapshot = await Task.Run(() => ApplyWork(work));
            StatusMessage = $"Applied {work.Count} change(s). Undo is available in Settings.";
            _ = snapshot;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Something went wrong: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
            LoadItems();
        }
    }

    private BackupSnapshot ApplyWork(List<(TweakDefinition Definition, bool IsOn)> work)
    {
        _backup.TryCreateRestorePoint($"Vishal X Opt - {TabName} tab");

        var snapshot = new BackupSnapshot { Description = $"{TabName} tab ({work.Count} change(s))" };

        foreach (var (definition, isOn) in work)
        {
            switch (definition.Kind)
            {
                case TweakKind.Registry:
                    snapshot.RegistryValues.AddRange(_registry.Apply(definition, isOn));
                    break;

                case TweakKind.Service when definition.ServiceName is not null:
                    var previous = _serviceControl.Apply(definition.ServiceName, isOn, definition.ServiceDefaultStart);
                    if (previous is not null)
                        snapshot.ServiceValues.Add(new BackupServiceValue { ServiceName = definition.ServiceName, PreviousStartMode = previous });
                    break;

                case TweakKind.Process:
                    var command = isOn ? definition.OnCommand : definition.OffCommand;
                    if (!string.IsNullOrWhiteSpace(command))
                    {
                        var parts = command.Split(' ', 2);
                        ProcessRunner.RunAndWait(parts[0], parts.Length > 1 ? parts[1] : "");
                    }
                    break;
            }
            snapshot.ChangedTweakIds.Add(definition.Id);
        }

        if (snapshot.RegistryValues.Count > 0 || snapshot.ServiceValues.Count > 0)
            _backup.Save(snapshot);

        return snapshot;
    }
}
