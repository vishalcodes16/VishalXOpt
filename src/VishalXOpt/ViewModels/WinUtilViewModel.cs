using System.Collections.ObjectModel;
using System.Windows;
using VishalXOpt.Mvvm;
using VishalXOpt.Services;

namespace VishalXOpt.ViewModels;

public sealed class WinUtilViewModel : ViewModelBase
{
    private readonly WinUtilService _service;
    private CancellationTokenSource? _cts;

    public string CommandText => WinUtilService.Command;
    public ObservableCollection<string> OutputLines { get; } = new();

    private bool _isRunning;
    public bool IsRunning
    {
        get => _isRunning;
        set => SetField(ref _isRunning, value);
    }

    public AsyncRelayCommand RunCommand { get; }
    public RelayCommand StopCommand { get; }
    public RelayCommand ClearCommand { get; }

    public WinUtilViewModel(WinUtilService service)
    {
        _service = service;
        RunCommand = new AsyncRelayCommand(RunAsync, () => !IsRunning);
        StopCommand = new RelayCommand(_ => _cts?.Cancel(), _ => IsRunning);
        ClearCommand = new RelayCommand(_ => OutputLines.Clear());
    }

    private async Task RunAsync()
    {
        IsRunning = true;
        _cts = new CancellationTokenSource();
        OutputLines.Add($"> {CommandText}");

        try
        {
            await _service.RunAsync(
                line => Application.Current.Dispatcher.Invoke(() => OutputLines.Add(line)),
                _cts.Token);
        }
        catch (OperationCanceledException)
        {
            OutputLines.Add("[cancelled]");
        }
        catch (Exception ex)
        {
            OutputLines.Add($"[error] {ex.Message}");
        }
        finally
        {
            IsRunning = false;
        }
    }
}
