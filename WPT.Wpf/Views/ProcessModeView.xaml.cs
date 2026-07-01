using System.Windows;
using WPT.Wpf.ViewModels;

namespace WPT.Wpf.Views;

public partial class ProcessModeView
{
    public ProcessModeView()
    {
        InitializeComponent();
    }

    private void OnAmneziaConfigDragOver(object sender, System.Windows.DragEventArgs e)
    {
        if (!IsAmneziaConfigDropEnabled())
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

    private void OnAmneziaConfigDrop(object sender, System.Windows.DragEventArgs e)
    {
        if (!IsAmneziaConfigDropEnabled() || !e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop))
        {
            return;
        }

        if (e.Data.GetData(System.Windows.DataFormats.FileDrop) is not string[] files || files.Length == 0)
        {
            return;
        }

        if (DataContext is MainViewModel viewModel)
        {
            viewModel.TryImportAmneziaConfigFile(files[0]);
        }
    }

    private bool IsAmneziaConfigDropEnabled() =>
        DataContext is MainViewModel { IsProcessModeEditingEnabled: true };

}
