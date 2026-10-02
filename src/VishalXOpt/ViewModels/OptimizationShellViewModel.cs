using VishalXOpt.Mvvm;
using VishalXOpt.Services;

namespace VishalXOpt.ViewModels;

public sealed record OptimizationTabInfo(string Id, string DisplayName, string Icon);

/// <summary>
/// Hosts the "Basic / Security / Customization / Power management / Debloat / Cleanup /
/// Privacy / Tweaks / Autoruns / Devices & Interrupts / Network adapters / Tasks / Components /
/// Deprecated" secondary sidebar and swaps <see cref="CurrentContent"/> between them. Each
/// tab's view model is created once, on first visit, and cached for the rest of the session.
/// </summary>
public sealed class OptimizationShellViewModel : ViewModelBase
{
    private readonly TweakCatalogService _catalog;
    private readonly RegistryTweakService _registry;
    private readonly ServiceControlService _serviceControl;
    private readonly BackupRestoreService _backup;
    private readonly CleanupService _cleanup;
    private readonly PowerCfgService _powerCfg;
    private readonly AutorunsService _autoruns;
    private readonly DebloatService _debloat;
    private readonly DeviceInterruptService _deviceInterrupt;
    private readonly NetAdapterService _netAdapter;
    private readonly TaskSchedulerService _taskScheduler;
    private readonly DismComponentService _dismComponent;
    private readonly WinUtilService _winUtil;

    private readonly Dictionary<string, object> _cache = new();

    public static readonly IReadOnlyList<OptimizationTabInfo> AllTabs = new List<OptimizationTabInfo>
    {
        new("WinUtil", "WinUtil", "\uE756"),
        new("Basic", "Basic", "\uE713"),
        new("Security", "Security", "\uE72E"),
        new("Customization", "Customization", "\uE70F"),
        new("PowerManagement", "Power management", "\uE83A"),
        new("Debloat", "Debloat", "\uE74D"),
        new("Cleanup", "Cleanup", "\uE9AD"),
        new("Privacy", "Privacy", "\uE72E"),
        new("Tweaks", "Tweaks", "\uE9F5"),
        new("Autoruns", "Autoruns", "\uE81C"),
        new("DevicesInterrupts", "Devices & Interrupts", "\uE950"),
        new("NetworkAdapters", "Network adapters", "\uE839"),
        new("Tasks", "Tasks", "\uE73A"),
        new("Components", "Components", "\uE7B8"),
        new("Deprecated", "Deprecated", "\uE7BA"),
    };

    /// <summary>Instance wrapper so XAML can bind to it - {Binding Tabs} cannot see a static
    /// field on the DataContext instance, only instance members.</summary>
    public IReadOnlyList<OptimizationTabInfo> Tabs => AllTabs;

    private OptimizationTabInfo _selectedTab = Tabs[0];
    public OptimizationTabInfo SelectedTab
    {
        get => _selectedTab;
        set
        {
            if (SetField(ref _selectedTab, value)) CurrentContent = Resolve(value.Id);
        }
    }

    private object? _currentContent;
    public object? CurrentContent
    {
        get => _currentContent;
        private set => SetField(ref _currentContent, value);
    }

    public OptimizationShellViewModel(
        TweakCatalogService catalog, RegistryTweakService registry, ServiceControlService serviceControl,
        BackupRestoreService backup, CleanupService cleanup, PowerCfgService powerCfg,
        AutorunsService autoruns, DebloatService debloat, DeviceInterruptService deviceInterrupt,
        NetAdapterService netAdapter, TaskSchedulerService taskScheduler, DismComponentService dismComponent,
        WinUtilService winUtil)
    {
        _catalog = catalog;
        _registry = registry;
        _serviceControl = serviceControl;
        _backup = backup;
        _cleanup = cleanup;
        _powerCfg = powerCfg;
        _autoruns = autoruns;
        _debloat = debloat;
        _deviceInterrupt = deviceInterrupt;
        _netAdapter = netAdapter;
        _taskScheduler = taskScheduler;
        _dismComponent = dismComponent;
        _winUtil = winUtil;

        CurrentContent = Resolve(_selectedTab.Id);
    }

    private object Resolve(string tabId)
    {
        if (_cache.TryGetValue(tabId, out var existing)) return existing;

        object vm = tabId switch
        {
            "Basic" or "Security" or "Customization" or "Privacy" or "Tweaks" or "Deprecated" =>
                new TweakTabViewModel(tabId, _catalog, _registry, _serviceControl, _backup),
            "WinUtil" => new WinUtilViewModel(_winUtil),
            "PowerManagement" => new PowerManagementViewModel(_powerCfg),
            "Debloat" => new DebloatViewModel(_debloat),
            "Cleanup" => new CleanupViewModel(_cleanup),
            "Autoruns" => new AutorunsViewModel(_autoruns),
            "DevicesInterrupts" => new DevicesInterruptsViewModel(_deviceInterrupt),
            "NetworkAdapters" => new NetworkAdaptersViewModel(_netAdapter),
            "Tasks" => new TasksViewModel(_taskScheduler),
            "Components" => new ComponentsViewModel(_dismComponent),
            _ => new object()
        };

        _cache[tabId] = vm;
        return vm;
    }
}
