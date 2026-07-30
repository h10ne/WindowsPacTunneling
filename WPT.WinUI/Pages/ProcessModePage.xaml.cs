using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using WPT.ViewModels;

namespace WPT.WinUI.Pages;

public sealed partial class ProcessModePage : Page
{
    public ProcessModePage()
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
