using VishalXOpt.Mvvm;
using VishalXOpt.Services;

namespace VishalXOpt.ViewModels;

public sealed class PowerManagementViewModel : ViewModelBase
{
    private readonly PowerCfgService _service;

    private string _activeScheme = "";
    public string ActiveScheme
    {
        get => _activeScheme;
        set => SetField(ref _activeScheme, value);
    }

    private string _statusMessage = "";
    public string StatusMessage
    {
        get => _statusMessage;
        set => SetField(ref _statusMessage, value);
    }

    private bool _isLaptop;
    /// <summary>Shown as a manual toggle rather than auto-detected battery presence, so the
    /// person always stays in control of whether the AC-only warning applies to them.</summary>
    public bool IsLaptop
    {
        get => _isLaptop;
        set => SetField(ref _isLaptop, value);
    }

    public RelayCommand RefreshCommand { get; }
    public RelayCommand EnableUltimatePerformanceCommand { get; }
    public RelayCommand RestoreBalancedCommand { get; }
    public RelayCommand MaxCpuStateCommand { get; }
    public RelayCommand DisableUsbSuspendCommand { get; }

    public PowerManagementViewModel(PowerCfgService service)
    {
        _service = service;
        RefreshCommand = new RelayCommand(_ => Refresh());
        EnableUltimatePerformanceCommand = new RelayCommand(_ => EnableUltimatePerformance());
        RestoreBalancedCommand = new RelayCommand(_ => RestoreBalanced());
        MaxCpuStateCommand = new RelayCommand(_ => SetMaxCpuState());
        DisableUsbSuspendCommand = new RelayCommand(_ => DisableUsbSuspend());
        Refresh();
    }

    private void Refresh() => ActiveScheme = _service.GetActiveSchemeName();

    private void EnableUltimatePerformance()
    {
        var ok = _service.EnableUltimatePerformance();
        if (!ok)
        {
            StatusMessage = "This PC doesn't expose the Ultimate Performance scheme (common on Modern Standby laptops). Nothing was changed.";
            return;
        }

        StatusMessage = IsLaptop
            ? "Ultimate Performance is active. Heads up: it disables most battery-saving behavior - switch back to Balanced when unplugged."
            : "Ultimate Performance plan is now active.";
        Refresh();
    }

    private void RestoreBalanced()
    {
        _service.RestoreBalanced();
        StatusMessage = "Balanced plan restored.";
        Refresh();
    }

    private void SetMaxCpuState()
    {
        if (IsLaptop)
        {
            StatusMessage = "Skipped: this laptop toggle is marked as a laptop, so the CPU minimum state was left alone to protect battery life.";
            return;
        }
        _service.SetMinProcessorState(100);
        StatusMessage = "CPU minimum state set to 100% (desktop-only setting).";
    }

    private void DisableUsbSuspend()
    {
        _service.DisableUsbSelectiveSuspend();
        StatusMessage = "USB selective suspend disabled for the active plan.";
    }
}
