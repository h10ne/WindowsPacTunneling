using WPT.Core.Services;
using WPT.TraySupport;
using WPT.ViewModels;
using WPT.ViewModels.Abstractions;
using WinUiApplication = Microsoft.UI.Xaml.Application;

namespace WPT.WinUI.Services;

public sealed class TrayService : IDisposable
{
    private readonly Microsoft.UI.Xaml.Window _window;
    private readonly TrayController _controller;
    private readonly MainViewModel _viewModel;

    public TrayService(Microsoft.UI.Xaml.Window window, MainViewModel viewModel)
    {
        _window = window;
        _viewModel = viewModel;
        var trayWindow = new WinUiTrayWindow(window);
        _controller = new TrayController(trayWindow, viewModel, AppIcon.CreateTrayIcon(), ExitApplication);
        SingleInstanceGuard.ListenForActivation(() => UiHostContext.Current.RunOnUiThread(_controller.ShowMainWindow));
    }

    public void UpdateMenu() => _controller.UpdateMenu();

    public void MinimizeToTray(bool showNotification) => _controller.MinimizeToTray(showNotification);

    public void ExitApplication()
    {
        _controller.AllowApplicationExit();
        _controller.Dispose();

        UiHostContext.Current.RunOnUiThread(() =>
        {
            WindowBoundsService.Capture(_window, _viewModel);
            _viewModel.Shutdown();
            _viewModel.StopLocalProxyOnExit();
            SingleInstanceGuard.Release();
            AppLog.Close();
            WinUiApplication.Current.Exit();
        });
    }

    public void Dispose() => _controller.Dispose();
}
