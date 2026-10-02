using System.Collections.ObjectModel;
using VishalXOpt.Mvvm;
using VishalXOpt.Services;

namespace VishalXOpt.ViewModels;

public sealed class ToolCardViewModel
{
    public string Title { get; init; } = "";
    public string Subtitle { get; init; } = "";
    public bool IsAvailable { get; init; }
    public string ButtonLabel { get; init; } = "Run";
    public RelayCommand? RunCommand { get; init; }
}

/// <summary>
/// Tools grid + drill-down. Three tools (GodMode, Internet test, Bottleneck) are simple one-shot
/// actions that just update <see cref="StatusMessage"/> in place. The other six (StoreX,
/// GameModeX, ProcessX, PC Latency Test, GameReadyX, Steam) are full dedicated screens - clicking
/// their card sets <see cref="CurrentDetail"/>, which the view swaps to; "Back" clears it again.
/// </summary>
public sealed class ToolsViewModel : ViewModelBase
{
    private readonly ToolsService _service;
    private readonly StoreXService _storeX;
    private readonly GameModeService _gameMode;
    private readonly ProcessAutomationService _processAutomation;
    private readonly LatencyTestService _latencyTest;
    private readonly GameReadyService _gameReady;
    private readonly SteamService _steam;

    public ObservableCollection<ToolCardViewModel> Cards { get; } = new();

    private object? _currentDetail;
    public object? CurrentDetail
    {
        get => _currentDetail;
        private set => SetField(ref _currentDetail, value);
    }

    public RelayCommand BackToGridCommand { get; }

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

    public ToolsViewModel(
        ToolsService service, StoreXService storeX, GameModeService gameMode,
        ProcessAutomationService processAutomation, LatencyTestService latencyTest,
        GameReadyService gameReady, SteamService steam)
    {
        _service = service;
        _storeX = storeX;
        _gameMode = gameMode;
        _processAutomation = processAutomation;
        _latencyTest = latencyTest;
        _gameReady = gameReady;
        _steam = steam;

        BackToGridCommand = new RelayCommand(_ => CurrentDetail = null);

        Cards.Add(new ToolCardViewModel
        {
            Title = "GodMode",
            Subtitle = "Creates a Desktop folder exposing every Control Panel setting in one view.",
            IsAvailable = true,
            RunCommand = new RelayCommand(_ => RunGodMode())
        });

        Cards.Add(new ToolCardViewModel
        {
            Title = "Internet test",
            Subtitle = "Pings a public resolver 10 times to measure latency, jitter and packet loss.",
            IsAvailable = true,
            RunCommand = new RelayCommand(async _ => await RunInternetTestAsync())
        });

        Cards.Add(new ToolCardViewModel
        {
            Title = "Bottleneck",
            Subtitle = "Samples live CPU vs. GPU utilization to flag which one is the current limiting factor.",
            IsAvailable = true,
            RunCommand = new RelayCommand(async _ => await RunBottleneckAsync())
        });

        Cards.Add(new ToolCardViewModel
        {
            Title = "StoreX",
            Subtitle = "Batch-install a curated set of everyday apps via winget.",
            IsAvailable = true,
            ButtonLabel = "Open",
            RunCommand = new RelayCommand(_ => CurrentDetail = new StoreXViewModel(_storeX, () => CurrentDetail = null))
        });

        Cards.Add(new ToolCardViewModel
        {
            Title = "GameModeX",
            Subtitle = "One-tap temporary \"aggressive gaming profile\" for the current session.",
            IsAvailable = true,
            ButtonLabel = "Open",
            RunCommand = new RelayCommand(_ => CurrentDetail = new GameModeXViewModel(_gameMode, () => CurrentDetail = null))
        });

        Cards.Add(new ToolCardViewModel
        {
            Title = "ProcessX",
            Subtitle = "Rule-based process priority automation - set it and forget it.",
            IsAvailable = true,
            ButtonLabel = "Open",
            RunCommand = new RelayCommand(_ => CurrentDetail = new ProcessXViewModel(_processAutomation, () => CurrentDetail = null))
        });

        Cards.Add(new ToolCardViewModel
        {
            Title = "PC Latency Test",
            Subtitle = "A real scheduler-jitter measurement (an approximation of full DPC/ISR tracing).",
            IsAvailable = true,
            ButtonLabel = "Open",
            RunCommand = new RelayCommand(_ => CurrentDetail = new LatencyTestViewModel(_latencyTest, () => CurrentDetail = null))
        });

        Cards.Add(new ToolCardViewModel
        {
            Title = "GameReadyX",
            Subtitle = "Pre-session checklist: pending reboot, driver age, background app count, Update activity.",
            IsAvailable = true,
            ButtonLabel = "Open",
            RunCommand = new RelayCommand(_ => CurrentDetail = new GameReadyXViewModel(_gameReady, () => CurrentDetail = null))
        });

        Cards.Add(new ToolCardViewModel
        {
            Title = "Steam",
            Subtitle = "Quick-launch shortcuts read from your local Steam library.",
            IsAvailable = true,
            ButtonLabel = "Open",
            RunCommand = new RelayCommand(_ => CurrentDetail = new SteamViewModel(_steam, () => CurrentDetail = null))
        });
    }

    private void RunGodMode()
    {
        var path = _service.CreateGodModeFolder();
        StatusMessage = $"GodMode folder created: {path}";
    }

    private async Task RunInternetTestAsync()
    {
        IsBusy = true;
        StatusMessage = "Pinging...";
        var result = await _service.RunInternetTestAsync();
        StatusMessage = $"Avg latency {result.AvgLatencyMs:0} ms - jitter {result.JitterMs:0.0} ms - loss {result.PacketLossPercent:0}%.";
        IsBusy = false;
    }

    private async Task RunBottleneckAsync()
    {
        IsBusy = true;
        StatusMessage = "Sampling CPU/GPU usage...";
        // PerformanceCounters need two samples ~0.5s apart, so keep the wait off the UI thread.
        var result = await Task.Run(() => _service.SampleBottleneck());
        StatusMessage = $"CPU {result.CpuPercent:0}% - GPU {result.GpuPercent:0}% - {result.Verdict}";
        IsBusy = false;
    }
}
