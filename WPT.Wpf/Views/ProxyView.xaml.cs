using WPT.Wpf.ViewModels;

namespace WPT.Wpf.Views;

public partial class ProxyView
{
    public ProxyView()
    {
        InitializeComponent();
    }

    private void OnAddConfigExpanderChanged(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is MainViewModel viewModel)
        {
            viewModel.ClearProxyConfigSaveNotice();
        }
    }

}
