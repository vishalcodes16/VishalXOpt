using System.Windows;
using System.Windows.Controls;
using VishalXOpt.ViewModels;

namespace VishalXOpt.Views;

public partial class ListTabView : UserControl
{
    public ListTabView()
    {
        InitializeComponent();
    }

    // Debloat/Components deliberately skip loading their (slower) data in the constructor -
    // this picks it up the moment the tab is actually shown, and harmlessly re-refreshes the
    // already-fast tabs (Autoruns/Tasks) too whenever the person navigates back to them.
    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is ListTabViewModelBase vm) vm.RefreshCommand.Execute(null);
    }
}
