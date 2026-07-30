using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using WPT.ViewModels;
using WinUiWindow = Microsoft.UI.Xaml.Window;

namespace WPT.WinUI.Services;

internal static class WindowBoundsService
{
    private const int DefaultWidth = 900;
    private const int DefaultHeight = 620;
    private const int MinWidth = 820;
    private const int MinHeight = 560;

    public static void Configure(WinUiWindow window)
    {
        if (window.AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.PreferredMinimumWidth = MinWidth;
            presenter.PreferredMinimumHeight = MinHeight;
        }
    }

    public static void Restore(WinUiWindow window, MainViewModel? viewModel = null)
    {
        var (width, height) = viewModel?.GetWindowBoundsOrDefault(DefaultWidth, DefaultHeight)
            ?? LoadBoundsFromSettings();
        window.AppWindow.Resize(new Windows.Graphics.SizeInt32(width, height));
    }

    public static void Capture(WinUiWindow window, MainViewModel? viewModel)
    {
        if (viewModel == null)
        {
            return;
        }

        var size = window.AppWindow.Size;
        viewModel.UpdateWindowBounds(size.Width, size.Height);
    }

    private static (int Width, int Height) LoadBoundsFromSettings()
    {
        var settings = WPT.Core.Services.SettingsService.Load();
        var width = settings.WindowWidth >= MinWidth ? settings.WindowWidth : DefaultWidth;
        var height = settings.WindowHeight >= MinHeight ? settings.WindowHeight : DefaultHeight;
        return (width, height);
    }

}
