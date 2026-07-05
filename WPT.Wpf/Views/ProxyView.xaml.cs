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

    private void OnVpnConfigDragOver(object sender, System.Windows.DragEventArgs e)
    {
        if (!IsVpnConfigDropEnabled())
        {
            e.Effects = System.Windows.DragDropEffects.None;
            e.Handled = true;
            return;
        }

        e.Effects = e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop)
            ? System.Windows.DragDropEffects.Copy
            : System.Windows.DragDropEffects.None;
        e.Handled = true;
    }

    private void OnVpnConfigDrop(object sender, System.Windows.DragEventArgs e)
    {
        if (!IsVpnConfigDropEnabled() || !e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop))
        {
            return;
        }

        if (e.Data.GetData(System.Windows.DataFormats.FileDrop) is not string[] files || files.Length == 0)
        {
            return;
        }

        if (DataContext is MainViewModel viewModel)
        {
            viewModel.TryImportVpnConfigFile(files[0]);
        }
    }

    private bool IsVpnConfigDropEnabled() =>
        DataContext is MainViewModel { IsProxyEditingEnabled: true };

}
