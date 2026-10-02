using System.Collections.ObjectModel;
using VishalXOpt.Models;
using VishalXOpt.Mvvm;
using VishalXOpt.Services;

namespace VishalXOpt.ViewModels;

public sealed class SettingsViewModel : ViewModelBase
{
    private readonly BackupRestoreService _backup;

    public ObservableCollection<BackupSnapshot> Snapshots { get; } = new();

    private string _statusMessage = "";
    public string StatusMessage
    {
        get => _statusMessage;
        set => SetField(ref _statusMessage, value);
    }

    public string AppVersion => "1.0.0";

    public RelayCommand RefreshCommand { get; }
    public RelayCommand UndoLastCommand { get; }

    public SettingsViewModel(BackupRestoreService backup)
    {
        _backup = backup;
        RefreshCommand = new RelayCommand(_ => Load());
        UndoLastCommand = new RelayCommand(_ => UndoLast());
        Load();
    }

    private void Load()
    {
        Snapshots.Clear();
        foreach (var snapshot in _backup.ListAll()) Snapshots.Add(snapshot);
        StatusMessage = Snapshots.Count == 0
            ? "No changes recorded yet - snapshots appear here after you Apply something."
            : $"{Snapshots.Count} snapshot(s) on record.";
    }

    private void UndoLast()
    {
        var ok = _backup.UndoLast();
        StatusMessage = ok
            ? "Reverted the most recent set of changes."
            : "Nothing to undo yet.";
    }
}
