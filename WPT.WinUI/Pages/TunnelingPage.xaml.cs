using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using WPT.ViewModels;

namespace WPT.WinUI.Pages;

public sealed partial class TunnelingPage : Page
{
    public TunnelingPage()
    {
        InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (e.Parameter is MainViewModel viewModel)
        {
            DataContext = viewModel;
        }
    }

}
