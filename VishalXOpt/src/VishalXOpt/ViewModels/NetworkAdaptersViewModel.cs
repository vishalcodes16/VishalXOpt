using System.Collections.ObjectModel;
using VishalXOpt.Models;
using VishalXOpt.Mvvm;
using VishalXOpt.Services;

namespace VishalXOpt.ViewModels;

/// <summary>One editable advanced-property row: a dropdown of the driver's own
/// ValidDisplayValues, applied immediately when changed.</summary>
public sealed class NetAdapterPropertyRowViewModel : ViewModelBase
{
    private readonly NetAdapterService _service;
    public NetAdapterPropertyInfo Info { get; }

    public string DisplayName => Info.DisplayName;
    public List<string> ValidDisplayValues => Info.ValidDisplayValues.Count > 0
        ? Info.ValidDisplayValues
        : new List<string> { Info.DisplayValue };

    private string _selectedValue;
    public string SelectedValue
    {
        get => _selectedValue;
        set
        {
            if (!SetField(ref _selectedValue, value)) return;
            if (value == Info.DisplayValue) return;
            _service.SetProperty(Info.AdapterName, Info.DisplayName, value);
            Info.DisplayValue = value;
        }
    }

    public NetAdapterPropertyRowViewModel(NetAdapterPropertyInfo info, NetAdapterService service)
    {
        Info = info;
        _service = service;
        _selectedValue = info.DisplayValue;
    }
}

public sealed class NetworkAdaptersViewModel : ViewModelBase
{
    private readonly NetAdapterService _service;

    public ObservableCollection<string> AdapterNames { get; } = new();
    public ObservableCollection<NetAdapterPropertyRowViewModel> Properties { get; } = new();

    private string? _selectedAdapter;
    public string? SelectedAdapter
    {
        get => _selectedAdapter;
        set
        {
            if (SetField(ref _selectedAdapter, value) && value is not null)
                _ = LoadPropertiesAsync(value);
        }
    }

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

    public AsyncRelayCommand RefreshAdaptersCommand { get; }
    public AsyncRelayCommand ApplyLowLatencyCommand { get; }

    public NetworkAdaptersViewModel(NetAdapterService service)
    {
        _service = service;
        RefreshAdaptersCommand = new AsyncRelayCommand(LoadAdaptersAsync);
        ApplyLowLatencyCommand = new AsyncRelayCommand(ApplyLowLatencyAsync);
    }

    /// <summary>Both the adapter list and each adapter's property list come from PowerShell
    /// (Get-NetAdapter / Get-NetAdapterAdvancedProperty) and take a second or two each, so they
    /// run off the UI thread. Triggered from the view's Loaded event.</summary>
    private async Task LoadAdaptersAsync()
    {
        IsBusy = true;
        StatusMessage = "Reading network adapters...";
        try
        {
            var names = await Task.Run(() => _service.ListAdapterNames());
            AdapterNames.Clear();
            foreach (var name in names) AdapterNames.Add(name);

            if (AdapterNames.Count == 0)
            {
                Properties.Clear();
                StatusMessage = "No network adapters found.";
                return;
            }

            // Re-select even if it's the same adapter, so its property list refreshes too.
            _selectedAdapter = null;
            SelectedAdapter = AdapterNames[0];
            OnPropertyChanged(nameof(SelectedAdapter));
        }
        catch (Exception ex)
        {
            StatusMessage = $"Could not read adapters: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task LoadPropertiesAsync(string adapterName)
    {
        StatusMessage = $"Loading {adapterName} properties...";
        try
        {
            var props = await Task.Run(() => _service.ListProperties(adapterName));
            if (SelectedAdapter != adapterName) return; // selection moved on while we were loading

            Properties.Clear();
            foreach (var prop in props) Properties.Add(new NetAdapterPropertyRowViewModel(prop, _service));
            StatusMessage = $"{Properties.Count} advanced propert(ies) for {adapterName}. Change a dropdown to apply it immediately.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Could not read properties: {ex.Message}";
        }
    }

    private async Task ApplyLowLatencyAsync()
    {
        var adapter = SelectedAdapter;
        if (adapter is null) return;

        IsBusy = true;
        StatusMessage = "Applying the Low Latency preset...";
        await Task.Run(() => _service.ApplyLowLatencyPreset(adapter));
        await LoadPropertiesAsync(adapter);
        StatusMessage = "Low Latency preset applied. " + StatusMessage;
        IsBusy = false;
    }
}
