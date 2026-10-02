using System.Collections.ObjectModel;
using VishalXOpt.Mvvm;
using VishalXOpt.Services;

namespace VishalXOpt.ViewModels;

public sealed class CleanupCategoryViewModel : ViewModelBase
{
    public CleanupCategory Category { get; }

    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set => SetField(ref _isSelected, value);
    }

    private string _sizeText = "Not scanned yet";
    public string SizeText
    {
        get => _sizeText;
        set => SetField(ref _sizeText, value);
    }

    public long SizeBytes { get; set; }

    public CleanupCategoryViewModel(CleanupCategory category)
    {
        Category = category;
        IsSelected = !category.IsOptIn; // event log clearing starts unchecked
    }
}

public sealed class CleanupViewModel : ViewModelBase
{
    private readonly CleanupService _service;

    public ObservableCollection<CleanupCategoryViewModel> Categories { get; } = new();

    private string _statusMessage = "Click \"Scan\" to see how much space you can free.";
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

    public AsyncRelayCommand ScanCommand { get; }
    public AsyncRelayCommand CleanSelectedCommand { get; }

    public CleanupViewModel(CleanupService service)
    {
        _service = service;
        foreach (var category in CleanupService.Categories)
            Categories.Add(new CleanupCategoryViewModel(category));

        ScanCommand = new AsyncRelayCommand(ScanAsync);
        CleanSelectedCommand = new AsyncRelayCommand(CleanSelectedAsync);
    }

    private async Task ScanAsync()
    {
        IsBusy = true;
        StatusMessage = "Scanning...";
        var ids = Categories.Select(c => c.Category.Id).ToList();

        // Directory walks can take a while on a big Temp/Update cache - keep them off the UI thread.
        var sizes = await Task.Run(() => ids.Select(id => _service.ScanCategory(id)).ToList());

        long total = 0;
        for (int i = 0; i < Categories.Count; i++)
        {
            Categories[i].SizeBytes = sizes[i];
            Categories[i].SizeText = FormatSize(sizes[i]);
            if (Categories[i].IsSelected) total += sizes[i];
        }
        StatusMessage = $"Scan complete - about {FormatSize(total)} can be freed from the checked categories.";
        IsBusy = false;
    }

    private async Task CleanSelectedAsync()
    {
        var selected = Categories.Where(c => c.IsSelected).Select(c => c.Category.Id).ToList();
        if (selected.Count == 0)
        {
            StatusMessage = "Check at least one category first.";
            return;
        }

        IsBusy = true;
        StatusMessage = "Cleaning...";
        await Task.Run(() =>
        {
            foreach (var id in selected) _service.Clean(id);
        });

        var count = selected.Count;
        IsBusy = false;
        await ScanAsync(); // re-scan so sizes reflect what's left
        StatusMessage = $"Cleaned {count} categor{(count == 1 ? "y" : "ies")}. " + StatusMessage;
    }

    private static string FormatSize(long bytes)
    {
        double mb = bytes / 1024.0 / 1024.0;
        return mb >= 1024 ? $"{mb / 1024:0.0} GB" : $"{mb:0.0} MB";
    }
}
