using System.Windows;
using System.Windows.Controls;
using VishalXOpt.ViewModels;

namespace VishalXOpt.Views;

public partial class NetworkAdaptersView : UserControl
{
    public NetworkAdaptersView()
    {
        InitializeComponent();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is NetworkAdaptersViewModel vm && vm.RefreshAdaptersCommand.CanExecute(null))
            vm.RefreshAdaptersCommand.Execute(null);
    }
}
