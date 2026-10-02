using System.Collections.ObjectModel;
using VishalXOpt.Models;
using VishalXOpt.Mvvm;
using VishalXOpt.Services;

namespace VishalXOpt.ViewModels;

/// <summary>One device row: an optional MSI-mode toggle, and (for a couple of well-known,
/// rarely-needed enumerators) an optional "disable this device entirely" toggle.</summary>
public sealed class DeviceRowViewModel : ViewModelBase
{
    private readonly DeviceInterruptService _service;
    public DeviceInfo Info { get; }

    public string Name => Info.Name;
    public bool MsiSupported => Info.MsiModeSupported;
    public bool ShowUnnecessaryToggle => Info.IsCommonlyUnnecessary;

    private bool _isMsiEnabled;
    public bool IsMsiEnabled
    {
        get => _isMsiEnabled;
        set
        {
            if (SetField(ref _isMsiEnabled, value)) _service.SetMsiMode(Info, value);
        }
    }

    private bool _isDeviceEnabled;
    public bool IsDeviceEnabled
    {
        get => _isDeviceEnabled;
        set
        {
            if (!SetField(ref _isDeviceEnabled, value)) return;
            if (value) _service.EnableDevice(Info); else _service.DisableDevice(Info);
        }
    }

    public DeviceRowViewModel(DeviceInfo info, DeviceInterruptService service)
    {
        Info = info;
        _service = service;
        _isMsiEnabled = info.MsiModeEnabled;
        _isDeviceEnabled = !info.IsDisabled; // backing field: no pnputil call on load
    }
}

/// <summary>One selectable logical CPU thread in the Interrupts affinity picker.</summary>
public sealed class ThreadOptionViewModel : ViewModelBase
{
    public int Index { get; }
    public string Label => $"Thread {Index}";

    private bool _isChecked;
    public bool IsChecked
    {
        get => _isChecked;
        set => SetField(ref _isChecked, value);
    }

    public ThreadOptionViewModel(int index) => Index = index;
}

public sealed class DevicesInterruptsViewModel : ViewModelBase
{
    private readonly DeviceInterruptService _service;

    public ObservableCollection<DeviceRowViewModel> Devices { get; } = new();
    public ObservableCollection<ThreadOptionViewModel> AffinityThreads { get; } = new();

    private DeviceRowViewModel? _selectedDevice;
    public DeviceRowViewModel? SelectedDevice
    {
        get => _selectedDevice;
        set => SetField(ref _selectedDevice, value);
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

    /// <summary>"Lock Interrupt Routing" - free here, unlike the Pro-gated original. While on,
    /// every affinity you pin is remembered and re-applied each time Vishal X Opt starts (see
    /// <see cref="DeviceInterruptService.ReapplyLockedAffinities"/>).</summary>
    private bool _lockInterruptRouting;
    public bool LockInterruptRouting
    {
        get => _lockInterruptRouting;
        set
        {
            if (!SetField(ref _lockInterruptRouting, value)) return;
            _service.SetLockEnabled(value);
            StatusMessage = value
                ? "Lock on - affinities you pin are remembered and re-applied every time this app starts."
                : "Lock off - pinned affinities stay in place but are no longer re-applied on launch.";
        }
    }

    public AsyncRelayCommand RefreshCommand { get; }
    public RelayCommand ApplyAffinityCommand { get; }
    public RelayCommand ClearAffinityCommand { get; }

    public DevicesInterruptsViewModel(DeviceInterruptService service)
    {
        _service = service;

        for (int i = 0; i < DeviceInterruptService.LogicalProcessorCount; i++)
            AffinityThreads.Add(new ThreadOptionViewModel(i));

        _lockInterruptRouting = service.IsLockEnabled(); // backing field: don't rewrite the file on load

        RefreshCommand = new AsyncRelayCommand(LoadAsync);
        ApplyAffinityCommand = new RelayCommand(_ => ApplyAffinity());
        ClearAffinityCommand = new RelayCommand(_ => ClearAffinity());
    }

    /// <summary>The WMI device query can take a second or two, so it runs off the UI thread.
    /// Called from the view's Loaded event.</summary>
    private async Task LoadAsync()
    {
        IsBusy = true;
        StatusMessage = "Reading devices...";
        try
        {
            var devices = await Task.Run(() => _service.ListDevices());
            Devices.Clear();
            foreach (var device in devices) Devices.Add(new DeviceRowViewModel(device, _service));
            SelectedDevice = Devices.FirstOrDefault();
            StatusMessage = $"{Devices.Count} device(s) found.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Could not read devices: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ApplyAffinity()
    {
        if (SelectedDevice is null) return;
        var threads = AffinityThreads.Where(t => t.IsChecked).Select(t => t.Index).ToList();
        if (threads.Count == 0)
        {
            StatusMessage = "Select at least one thread first.";
            return;
        }

        _service.SetInterruptAffinity(SelectedDevice.Info, threads);
        if (LockInterruptRouting) _service.RememberAffinity(SelectedDevice.Info, threads);

        StatusMessage = $"Pinned {SelectedDevice.Name} to thread(s) {string.Join(", ", threads)}. " +
                        "A device restart (or reboot) is needed for Windows to pick it up.";
    }

    private void ClearAffinity()
    {
        if (SelectedDevice is null) return;
        _service.ClearInterruptAffinity(SelectedDevice.Info);
        _service.ForgetAffinity(SelectedDevice.Info);
        foreach (var t in AffinityThreads) t.IsChecked = false;
        StatusMessage = $"Cleared pinned affinity for {SelectedDevice.Name} (back to the Windows default).";
    }
}
