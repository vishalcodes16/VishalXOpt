using System.Collections.ObjectModel;
using System.Diagnostics;
using VishalXOpt.Models;
using VishalXOpt.Mvvm;
using VishalXOpt.Services;

namespace VishalXOpt.ViewModels;

// ===================== StoreX =====================

public sealed class StoreXAppRowViewModel : ViewModelBase
{
    public WingetAppInfo App { get; }

    private bool _isChecked;
    public bool IsChecked
    {
        get => _isChecked;
        set => SetField(ref _isChecked, value);
    }

    private string _status = "";
    public string Status
    {
        get => _status;
        set => SetField(ref _status, value);
    }

    public StoreXAppRowViewModel(WingetAppInfo app) => App = app;
}

public sealed class StoreXViewModel : ViewModelBase
{
    private readonly StoreXService _service;

    public ObservableCollection<StoreXAppRowViewModel> Apps { get; } = new();
    public RelayCommand BackCommand { get; }
    public AsyncRelayCommand InstallSelectedCommand { get; }

    private bool _isBusy;
    public bool IsBusy
    {
        get => _isBusy;
        set => SetField(ref _isBusy, value);
    }

    private string _statusMessage;
    public string StatusMessage
    {
        get => _statusMessage;
        set => SetField(ref _statusMessage, value);
    }

    public StoreXViewModel(StoreXService service, Action back)
    {
        _service = service;
        BackCommand = new RelayCommand(_ => back());
        InstallSelectedCommand = new AsyncRelayCommand(InstallSelectedAsync);

        foreach (var app in StoreXService.CuratedApps) Apps.Add(new StoreXAppRowViewModel(app));

        _statusMessage = service.IsWingetAvailable()
            ? "Check the apps you want, then Install selected."
            : "winget wasn't found - install \"App Installer\" from the Microsoft Store, then come back here.";
    }

    private async Task InstallSelectedAsync()
    {
        var selected = Apps.Where(a => a.IsChecked).ToList();
        if (selected.Count == 0) { StatusMessage = "Check at least one app first."; return; }

        IsBusy = true;
        foreach (var row in selected)
        {
            row.Status = "Installing...";
            var ok = await Task.Run(() => _service.Install(row.App));
            row.Status = ok ? "Installed" : "Failed - see winget's own log for details";
        }
        StatusMessage = "Done.";
        IsBusy = false;
    }
}

// ===================== GameModeX =====================

public sealed class GameModeXViewModel : ViewModelBase
{
    private readonly GameModeService _service;

    public RelayCommand BackCommand { get; }
    public RelayCommand ToggleCommand { get; }

    public bool IsActive => _service.IsActive;

    private string _statusMessage = "Off. Turning this on boosts whatever's in the foreground, throttles common background apps, and switches to Ultimate Performance for the session.";
    public string StatusMessage
    {
        get => _statusMessage;
        set => SetField(ref _statusMessage, value);
    }

    public GameModeXViewModel(GameModeService service, Action back)
    {
        _service = service;
        BackCommand = new RelayCommand(_ => back());
        ToggleCommand = new RelayCommand(_ => Toggle());
    }

    private void Toggle()
    {
        if (_service.IsActive)
        {
            _service.Disable();
            StatusMessage = "Off - every priority it touched and the power plan have been restored.";
        }
        else
        {
            _service.Enable();
            StatusMessage = "Active - boosting the foreground app every few seconds, background apps throttled, Ultimate Performance on.";
        }
        OnPropertyChanged(nameof(IsActive));
    }
}

// ===================== ProcessX =====================

public sealed class ProcessRuleRowViewModel : ViewModelBase
{
    private readonly ProcessAutomationService _service;
    public ProcessRule Rule { get; }

    public string ProcessName => Rule.ProcessName;
    public string PriorityText => Rule.Priority.ToString();

    private bool _isEnabled;
    public bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            if (SetField(ref _isEnabled, value)) _service.SetRuleEnabled(Rule, value);
        }
    }

    public RelayCommand RemoveCommand { get; }

    public ProcessRuleRowViewModel(ProcessRule rule, ProcessAutomationService service, Action onRemoved)
    {
        Rule = rule;
        _service = service;
        _isEnabled = rule.Enabled;
        RemoveCommand = new RelayCommand(_ => { service.RemoveRule(rule); onRemoved(); });
    }
}

public sealed class ProcessXViewModel : ViewModelBase
{
    private readonly ProcessAutomationService _service;

    public ObservableCollection<ProcessRuleRowViewModel> Rules { get; } = new();
    public List<string> PriorityOptions { get; } = Enum.GetNames(typeof(ProcessPriorityClass)).ToList();

    private string _newProcessName = "";
    public string NewProcessName
    {
        get => _newProcessName;
        set => SetField(ref _newProcessName, value);
    }

    private string _newPriority = nameof(ProcessPriorityClass.BelowNormal);
    public string NewPriority
    {
        get => _newPriority;
        set => SetField(ref _newPriority, value);
    }

    public bool IsRunning => _service.IsRunning;

    private string _statusMessage = "Add a rule (e.g. \"Discord\" -> Below Normal), then start automation.";
    public string StatusMessage
    {
        get => _statusMessage;
        set => SetField(ref _statusMessage, value);
    }

    public RelayCommand BackCommand { get; }
    public RelayCommand AddRuleCommand { get; }
    public RelayCommand ToggleAutomationCommand { get; }
    public RelayCommand ApplyOnceCommand { get; }

    public ProcessXViewModel(ProcessAutomationService service, Action back)
    {
        _service = service;
        BackCommand = new RelayCommand(_ => back());
        AddRuleCommand = new RelayCommand(_ => AddRule());
        ToggleAutomationCommand = new RelayCommand(_ => ToggleAutomation());
        ApplyOnceCommand = new RelayCommand(_ => { service.ApplyRulesOnce(); StatusMessage = "Applied the current rules once."; });
        LoadRules();
    }

    private void LoadRules()
    {
        Rules.Clear();
        foreach (var rule in _service.Rules) Rules.Add(new ProcessRuleRowViewModel(rule, _service, LoadRules));
    }

    private void AddRule()
    {
        if (string.IsNullOrWhiteSpace(NewProcessName))
        {
            StatusMessage = "Enter a process name first (without .exe), e.g. \"Discord\".";
            return;
        }
        if (!Enum.TryParse<ProcessPriorityClass>(NewPriority, out var priority))
            priority = ProcessPriorityClass.BelowNormal;

        _service.AddRule(NewProcessName.Trim(), priority);
        NewProcessName = "";
        LoadRules();
        StatusMessage = "Rule added.";
    }

    private void ToggleAutomation()
    {
        if (_service.IsRunning)
        {
            _service.Stop();
            StatusMessage = "Automation stopped.";
        }
        else
        {
            _service.Start();
            StatusMessage = "Automation running - re-applying your rules every few seconds.";
        }
        OnPropertyChanged(nameof(IsRunning));
    }
}

// ===================== PC Latency Test =====================

public sealed class LatencyTestViewModel : ViewModelBase
{
    private readonly LatencyTestService _service;

    public RelayCommand BackCommand { get; }
    public AsyncRelayCommand RunCommand { get; }

    private bool _isBusy;
    public bool IsBusy
    {
        get => _isBusy;
        set => SetField(ref _isBusy, value);
    }

    private string _resultText = "This measures scheduler jitter (how late a 1ms sleep actually wakes up), sampled for 3 seconds - a real, if approximate, stand-in for full DPC/ISR latency tracing. Click Run.";
    public string ResultText
    {
        get => _resultText;
        set => SetField(ref _resultText, value);
    }

    public LatencyTestViewModel(LatencyTestService service, Action back)
    {
        _service = service;
        BackCommand = new RelayCommand(_ => back());
        RunCommand = new AsyncRelayCommand(RunAsync);
    }

    private async Task RunAsync()
    {
        IsBusy = true;
        ResultText = "Sampling for 3 seconds - avoid heavily loading the CPU right now for the cleanest read...";
        var result = await Task.Run(() => _service.RunSchedulerJitterTest());

        var verdict = result.AvgGapMs < 0.5 ? "Looks smooth."
            : result.AvgGapMs < 2 ? "Some jitter present - fairly normal under light background load."
            : "Noticeable jitter - check Task Manager for a process spiking CPU, or a driver/power-plan issue.";

        ResultText = $"Avg extra delay: {result.AvgGapMs:0.00} ms  |  Max: {result.MaxGapMs:0.00} ms  |  " +
                     $"Spikes over 2ms: {result.SpikesOver2Ms}/{result.SampleCount}\n\n{verdict}";
        IsBusy = false;
    }
}

// ===================== GameReadyX =====================

public sealed class GameReadyCheckRowViewModel
{
    public string Title { get; }
    public string Detail { get; }
    public CheckStatus Status { get; }

    public GameReadyCheckRowViewModel(GameReadyCheck check)
    {
        Title = check.Title;
        Detail = check.Detail;
        Status = check.Status;
    }
}

public sealed class GameReadyXViewModel : ViewModelBase
{
    private readonly GameReadyService _service;

    public ObservableCollection<GameReadyCheckRowViewModel> Checks { get; } = new();
    public RelayCommand BackCommand { get; }
    public AsyncRelayCommand RunCommand { get; }

    private bool _isBusy;
    public bool IsBusy
    {
        get => _isBusy;
        set => SetField(ref _isBusy, value);
    }

    private string _statusMessage = "Click Run checks before a session.";
    public string StatusMessage
    {
        get => _statusMessage;
        set => SetField(ref _statusMessage, value);
    }

    public GameReadyXViewModel(GameReadyService service, Action back)
    {
        _service = service;
        BackCommand = new RelayCommand(_ => back());
        RunCommand = new AsyncRelayCommand(RunAsync);
    }

    private async Task RunAsync()
    {
        IsBusy = true;
        StatusMessage = "Running checks...";
        var results = await Task.Run(() => _service.RunChecks());
        Checks.Clear();
        foreach (var r in results) Checks.Add(new GameReadyCheckRowViewModel(r));
        StatusMessage = $"{Checks.Count} check(s) complete.";
        IsBusy = false;
    }
}

// ===================== Steam =====================

public sealed class SteamGameRowViewModel
{
    public string Name { get; }
    public string AppId { get; }
    public RelayCommand LaunchCommand { get; }

    public SteamGameRowViewModel(SteamGameInfo game, SteamService service)
    {
        Name = game.Name;
        AppId = game.AppId;
        LaunchCommand = new RelayCommand(_ => service.LaunchGame(game.AppId));
    }
}

public sealed class SteamViewModel : ViewModelBase
{
    private readonly SteamService _service;

    public ObservableCollection<SteamGameRowViewModel> Games { get; } = new();
    public RelayCommand BackCommand { get; }
    public AsyncRelayCommand RefreshCommand { get; }

    private bool _isBusy;
    public bool IsBusy
    {
        get => _isBusy;
        set => SetField(ref _isBusy, value);
    }

    private string _statusMessage = "";
    public string StatusMessage
    {
        get => _statusMessage;
        set => SetField(ref _statusMessage, value);
    }

    public SteamViewModel(SteamService service, Action back)
    {
        _service = service;
        BackCommand = new RelayCommand(_ => back());
        RefreshCommand = new AsyncRelayCommand(RefreshAsync);
        _ = RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        IsBusy = true;
        StatusMessage = "Looking for a local Steam install...";
        var games = await Task.Run(() => _service.ListInstalledGames());
        Games.Clear();
        foreach (var g in games) Games.Add(new SteamGameRowViewModel(g, _service));
        StatusMessage = Games.Count == 0
            ? "No Steam installation or games found on this PC."
            : $"{Games.Count} installed game(s) found.";
        IsBusy = false;
    }
}
