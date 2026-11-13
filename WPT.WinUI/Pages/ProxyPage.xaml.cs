using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using Windows.ApplicationModel.DataTransfer;
using WPT.ViewModels;

namespace WPT.WinUI.Pages;

public sealed partial class ProxyPage : Page
{
    public ProxyPage()
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

    private void OnAddConfigPivotChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DataContext is MainViewModel viewModel)
        {
            viewModel.ClearProxyConfigSaveNotice();
        }
    }

    private void OnVpnConfigDragOver(object sender, DragEventArgs e)
    {
        if (!IsVpnConfigDropEnabled())
        {
            e.AcceptedOperation = DataPackageOperation.None;
            e.Handled = true;
            return;
        }

        e.AcceptedOperation = e.DataView.Contains(StandardDataFormats.StorageItems)
            ? DataPackageOperation.Copy
            : DataPackageOperation.None;
        e.Handled = true;
    }

    private async void OnVpnConfigDrop(object sender, DragEventArgs e)
    {
        if (!IsVpnConfigDropEnabled() || !e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            return;
        }

        var items = await e.DataView.GetStorageItemsAsync();
        if (items.Count == 0 || DataContext is not MainViewModel viewModel)
        {
            return;
        }

        viewModel.TryImportVpnConfigFile(items[0].Path);
    }

    private void OnVpnConfigDropZoneTapped(object sender, TappedRoutedEventArgs e) => PickVpnConfigFile();

    private void OnPickVpnConfigClick(object sender, RoutedEventArgs e) => PickVpnConfigFile();

    private void PickVpnConfigFile()
    {
        if (!IsVpnConfigDropEnabled() || DataContext is not MainViewModel viewModel)
        {
            return;
        }

        var path = App.UiHost.PickOpenFile("Выберите конфиг AmneziaWG", "*.conf|*.conf");
        if (!string.IsNullOrWhiteSpace(path))
        {
            viewModel.TryImportVpnConfigFile(path);
        }
    }

    private bool IsVpnConfigDropEnabled() =>
        DataContext is MainViewModel { IsProxyEditingEnabled: true };

}
