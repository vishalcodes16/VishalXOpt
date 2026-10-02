using System.Windows;
using System.Windows.Controls;
using VishalXOpt.ViewModels;

namespace VishalXOpt.Views;

public partial class DevicesInterruptsView : UserControl
{
    public DevicesInterruptsView()
    {
        InitializeComponent();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is DevicesInterruptsViewModel vm && vm.RefreshCommand.CanExecute(null))
            vm.RefreshCommand.Execute(null);
    }

    private void OnDeviceRowClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: DeviceRowViewModel row } &&
            DataContext is DevicesInterruptsViewModel vm)
        {
            vm.SelectedDevice = row;
        }
    }
}
