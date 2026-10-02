using System.Collections.ObjectModel;
using System.IO;
using VishalXOpt.Mvvm;
using VishalXOpt.Services;
using VishalXOpt.Models;

namespace VishalXOpt.ViewModels;

/// <summary>One row in a <c>ListTabView</c>: a name/description pair, an optional on/off
/// toggle that applies immediately, and an optional extra action button (e.g. "Delete").</summary>
public sealed class ListTabItemViewModel : ViewModelBase
{
    private readonly Action<bool>? _onToggle;

    public string Title { get; }
    public string Subtitle { get; }
    public bool HasToggle { get; }
    public string? ExtraActionLabel { get; }
    public RelayCommand? ExtraCommand { get; }

    private bool _isEnabled;
    public bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            if (SetField(ref _isEnabled, value)) _onToggle?.Invoke(value);
        }
    }

    public ListTabItemViewModel(
        string title, string subtitle, bool isEnabled, bool hasToggle,
        Action<bool>? onToggle = null, string? extraActionLabel = null, Action? extraAction = null)
    {
        Title = title;
        Subtitle = subtitle;
        _isEnabled = isEnabled;
        HasToggle = hasToggle;
        _onToggle = onToggle;
        ExtraActionLabel = extraActionLabel;
        ExtraCommand = extraAction is null ? null : new RelayCommand(_ => extraAction());
    }
}

/// <summary>Base for every simple "list of rows" tab: Autoruns, Debloat, Tasks, Components.
/// Unlike <see cref="TweakTabViewModel"/>, per-row changes apply immediately (deleting an
/// autorun entry, disabling a task, removing an app) rather than being staged behind an Apply
/// button - there's nothing to "preview" for those. The (potentially slow) initial <em>list
/// load</em>, however, always runs on a background thread via <see cref="BuildItemsAsync"/> so
/// opening Debloat/Components never freezes the UI.</summary>
public abstract class ListTabViewModelBase : ViewModelBase
{
    public string TabTitle { get; }
    public ObservableCollection<ListTabItemViewModel> Items { get; } = new();

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

    public AsyncRelayCommand RefreshCommand { get; }

    /// <summary>Optional extra tab-level action buttons (e.g. Autoruns' "Backup all" /
    /// "Restore latest backup"). Null on tabs that don't need one - the view hides the button.</summary>
    public string? PrimaryActionLabel { get; protected set; }
    public RelayCommand? PrimaryActionCommand { get; protected set; }
    public string? SecondaryActionLabel { get; protected set; }
    public RelayCommand? SecondaryActionCommand { get; protected set; }

    protected ListTabViewModelBase(string tabTitle)
    {
        TabTitle = tabTitle;
        RefreshCommand = new AsyncRelayCommand(RefreshAsync);
    }

    /// <summary>Runs <see cref="BuildItemsAsync"/> on a background thread (via <c>Task.Run</c>),
    /// then hands the finished list back to the UI thread to populate <see cref="Items"/>. WPF's
    /// synchronization context brings the continuation back to the UI thread automatically since
    /// this is awaited from a UI-thread-created command.</summary>
    private async Task RefreshAsync()
    {
        IsBusy = true;
        StatusMessage = "Loading...";
        try
        {
            var built = await Task.Run(() => BuildItemsAsync());
            Items.Clear();
            foreach (var item in built) Items.Add(item);
            StatusMessage = $"{Items.Count} item(s).";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Could not load: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>Runs on a background thread pool thread - build the full item list here (this is
    /// where the slow PowerShell/DISM/schtasks calls happen) but never touch <see cref="Items"/>
    /// directly from inside this method.</summary>
    protected abstract List<ListTabItemViewModel> BuildItemsAsync();

    /// <summary>Fire-and-forget re-load, used by row-level actions (Delete/Remove/toggle) that
    /// want the list to reflect the change they just made.</summary>
    protected void RequestRefresh() => RefreshCommand.Execute(null);
}

/// <summary>Autoruns tab - registry Run-key AND Startup-folder autostart entries.</summary>
public sealed class AutorunsViewModel : ListTabViewModelBase
{
    private readonly AutorunsService _service;

    public AutorunsViewModel(AutorunsService service) : base("Autoruns")
    {
        _service = service;
        PrimaryActionLabel = "Backup startup list";
        PrimaryActionCommand = new RelayCommand(_ =>
        {
            var path = _service.BackupToFile();
            StatusMessage = $"Backed up to {path}";
        });
        SecondaryActionLabel = "Restore latest backup";
        SecondaryActionCommand = new RelayCommand(_ =>
        {
            var path = _service.FindLatestBackup();
            if (path is null) { StatusMessage = "No backup found yet."; return; }
            var (restored, skipped) = _service.RestoreFromFile(path);
            StatusMessage = skipped == 0
                ? $"Restored {restored} entr{(restored == 1 ? "y" : "ies")} from {Path.GetFileName(path)}."
                : $"Restored {restored} entr{(restored == 1 ? "y" : "ies")}; {skipped} Startup-folder entr{(skipped == 1 ? "y" : "ies")} couldn't be recreated (the original file is gone).";
            RequestRefresh();
        });
        RequestRefresh(); // fast (registry + two folders) - safe to load eagerly
    }

    protected override List<ListTabItemViewModel> BuildItemsAsync()
    {
        var items = new List<ListTabItemViewModel>();
        foreach (var entry in _service.Enumerate())
        {
            var captured = entry;
            var subtitle = captured.Source == AutorunSourceKind.StartupFolder
                ? $"Startup folder - {captured.Command}"
                : captured.Command;

            items.Add(new ListTabItemViewModel(
                captured.Name,
                subtitle,
                captured.IsEnabled,
                hasToggle: true,
                onToggle: v => _service.SetEnabled(captured, v),
                extraActionLabel: "Delete",
                extraAction: () => { _service.Delete(captured); RequestRefresh(); }));
        }
        return items;
    }
}

/// <summary>Debloat tab - removable UWP/AppX packages, each with an approximate size.</summary>
public sealed class DebloatViewModel : ListTabViewModelBase
{
    private readonly DebloatService _service;

    public DebloatViewModel(DebloatService service) : base("Debloat")
    {
        _service = service;
        // The view's Loaded event triggers RefreshCommand once the tab is actually on screen -
        // no need to guard against a slow constructor any more since loading is async anyway,
        // but there's still no reason to size every package before the tab is even visited.
    }

    protected override List<ListTabItemViewModel> BuildItemsAsync()
    {
        var items = new List<ListTabItemViewModel>();
        foreach (var app in _service.ListRemovableApps())
        {
            var captured = app;
            items.Add(new ListTabItemViewModel(
                captured.Name,
                $"{captured.SizeMb:0.0} MB",
                isEnabled: false,
                hasToggle: false,
                extraActionLabel: "Remove",
                extraAction: () => { _service.Remove(captured); RequestRefresh(); }));
        }
        return items;
    }
}

/// <summary>Tasks tab - a curated set of built-in Windows scheduled tasks.</summary>
public sealed class TasksViewModel : ListTabViewModelBase
{
    private readonly TaskSchedulerService _service;

    public TasksViewModel(TaskSchedulerService service) : base("Tasks")
    {
        _service = service;
        RequestRefresh();
    }

    protected override List<ListTabItemViewModel> BuildItemsAsync()
    {
        var items = new List<ListTabItemViewModel>();
        foreach (var task in _service.ListTasks())
        {
            var captured = task;
            items.Add(new ListTabItemViewModel(
                captured.DisplayName,
                captured.Description,
                captured.IsEnabled,
                hasToggle: true,
                onToggle: v => _service.SetEnabled(captured, v)));
        }
        return items;
    }
}

/// <summary>Components tab - a curated set of Windows optional features.</summary>
public sealed class ComponentsViewModel : ListTabViewModelBase
{
    private readonly DismComponentService _service;

    public ComponentsViewModel(DismComponentService service) : base("Components")
    {
        _service = service;
    }

    protected override List<ListTabItemViewModel> BuildItemsAsync()
    {
        var items = new List<ListTabItemViewModel>();
        foreach (var feature in _service.ListFeatures())
        {
            var captured = feature;
            items.Add(new ListTabItemViewModel(
                captured.DisplayName,
                $"State: {captured.State}",
                isEnabled: captured.State.Equals("Enabled", StringComparison.OrdinalIgnoreCase),
                hasToggle: true,
                onToggle: v => { _service.SetEnabled(captured, v); RequestRefresh(); }));
        }
        return items;
    }
}
