using VishalXOpt.Mvvm;
using VishalXOpt.Services;

namespace VishalXOpt.ViewModels;

public enum MainSection
{
    Home,
    Optimization,
    Tools,
    Settings
}

/// <summary>
/// The app's composition root: every service is a plain, dependency-free class, so they are
/// all just constructed here once and handed down to whichever view model needs them - no DI
/// container required for an app this size.
/// </summary>
public sealed class MainViewModel : ViewModelBase, IDisposable
{
    // Services
    private readonly TweakCatalogService _catalog = new();
    private readonly RegistryTweakService _registry = new();
    private readonly ServiceControlService _serviceControl = new();
    private readonly BackupRestoreService _backup;
    private readonly CleanupService _cleanup = new();
    private readonly PowerCfgService _powerCfg = new();
    private readonly AutorunsService _autoruns = new();
    private readonly DebloatService _debloat = new();
    private readonly DeviceInterruptService _deviceInterrupt = new();
    private readonly NetAdapterService _netAdapter = new();
    private readonly TaskSchedulerService _taskScheduler = new();
    private readonly DismComponentService _dismComponent = new();
    private readonly WinUtilService _winUtil = new();
    private readonly ToolsService _tools = new();
    private readonly StoreXService _storeX = new();
    private readonly GameModeService _gameMode;
    private readonly ProcessAutomationService _processAutomation = new();
    private readonly LatencyTestService _latencyTest = new();
    private readonly GameReadyService _gameReady = new();
    private readonly SteamService _steam = new();

    // Child view models (created once, kept alive for the whole session)
    public HomeViewModel Home { get; }
    public OptimizationShellViewModel Optimization { get; }
    public ToolsViewModel Tools { get; }
    public SettingsViewModel Settings { get; }

    private MainSection _currentSection = MainSection.Home;
    public MainSection CurrentSection
    {
        get => _currentSection;
        set
        {
            if (!SetField(ref _currentSection, value)) return;
            OnPropertyChanged(nameof(IsHomeSelected));
            OnPropertyChanged(nameof(IsOptimizationSelected));
            OnPropertyChanged(nameof(IsToolsSelected));
            OnPropertyChanged(nameof(IsSettingsSelected));
        }
    }

    public bool IsHomeSelected => CurrentSection == MainSection.Home;
    public bool IsOptimizationSelected => CurrentSection == MainSection.Optimization;
    public bool IsToolsSelected => CurrentSection == MainSection.Tools;
    public bool IsSettingsSelected => CurrentSection == MainSection.Settings;

    public RelayCommand GoHomeCommand { get; }
    public RelayCommand GoOptimizationCommand { get; }
    public RelayCommand GoToolsCommand { get; }
    public RelayCommand GoSettingsCommand { get; }

    public MainViewModel()
    {
        _backup = new BackupRestoreService(_registry, _serviceControl);
        _gameMode = new GameModeService(_powerCfg);

        _catalog.Load();

        Optimization = new OptimizationShellViewModel(
            _catalog, _registry, _serviceControl, _backup, _cleanup, _powerCfg,
            _autoruns, _debloat, _deviceInterrupt, _netAdapter, _taskScheduler, _dismComponent, _winUtil);

        Tools = new ToolsViewModel(_tools, _storeX, _gameMode, _processAutomation, _latencyTest, _gameReady, _steam);
        Settings = new SettingsViewModel(_backup);
        Home = new HomeViewModel(_catalog, _registry,
            goToOptimization: () => CurrentSection = MainSection.Optimization,
            goToTools: () => CurrentSection = MainSection.Tools);

        GoHomeCommand = new RelayCommand(_ => CurrentSection = MainSection.Home);
        GoOptimizationCommand = new RelayCommand(_ => CurrentSection = MainSection.Optimization);
        GoToolsCommand = new RelayCommand(_ => CurrentSection = MainSection.Tools);
        GoSettingsCommand = new RelayCommand(_ => CurrentSection = MainSection.Settings);

        // "Lock Interrupt Routing": re-apply any affinities pinned in a previous session. A
        // handful of registry writes at most - runs off the UI thread so a slow WMI-backed device
        // lookup inside it never delays the window appearing.
        _ = Task.Run(() => { try { _deviceInterrupt.ReapplyLockedAffinities(); } catch { /* best-effort */ } });
    }

    /// <summary>Stops GameModeX/ProcessX's background timers and restores anything GameModeX had
    /// temporarily changed, so closing the app doesn't leave a process pinned at High priority
    /// or the power plan stuck on Ultimate Performance forever.</summary>
    public void Dispose()
    {
        if (_gameMode.IsActive) _gameMode.Disable();
        _gameMode.Dispose();
        _processAutomation.Dispose();
    }
}
