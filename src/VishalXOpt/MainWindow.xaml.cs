using System.ComponentModel;
using System.Windows;
using VishalXOpt.ViewModels;

namespace VishalXOpt;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    // Stops GameModeX/ProcessX's background timers and undoes anything GameModeX had
    // temporarily changed, so closing the window doesn't leave a process pinned at High
    // priority or the power plan stuck on Ultimate Performance.
    private void OnClosing(object sender, CancelEventArgs e)
    {
        if (DataContext is MainViewModel vm) vm.Dispose();
    }
}
