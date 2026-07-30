using Microsoft.UI.Xaml;
using WPT.TraySupport;
using WPT.ViewModels.Abstractions;

namespace WPT.WinUI.Services;

public sealed class WinUiTrayWindow : ITrayWindow
{
    private readonly Window _window;
    private Func<bool>? _closeHandler;

    public WinUiTrayWindow(Window window) => _window = window;

    public void Show() => _window.AppWindow.Show();

    public void Hide() => _window.AppWindow.Hide();

    public void Activate() => _window.Activate();

    public void AttachClosing(Func<bool> cancelCloseHandler)
    {
        _closeHandler = cancelCloseHandler;
        _window.AppWindow.Closing += AppWindow_Closing;
    }

    public void RunOnUiThread(Action action) => UiHostContext.Current.RunOnUiThread(action);

    private void AppWindow_Closing(Microsoft.UI.Windowing.AppWindow sender, Microsoft.UI.Windowing.AppWindowClosingEventArgs args)
    {
        if (_closeHandler == null)
        {
            return;
        }

        args.Cancel = _closeHandler();
    }

}
