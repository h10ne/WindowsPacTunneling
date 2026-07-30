using System.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WPT.Core;
using WPT.Core.Services;
using WPT.ViewModels;
using WPT.WinUI.Pages;
using WPT.WinUI.Services;

namespace WPT.WinUI;

public sealed partial class MainWindow : Window
{
    private TrayService? _trayService;
    private bool _isSyncingNavigation;

    public MainViewModel ViewModel { get; private set; } = null!;

    public MainWindow()
    {
        InitializeComponent();
        AppIcon.ApplyTo(this);
        Title = AppBranding.DisplayName;
        WindowBoundsService.Configure(this);
        WindowTitleBarHelper.WatchTheme(this, RootGrid);
        AppWindow.Closing += (_, _) => WindowBoundsService.Capture(this, ViewModel);
    }

    public void InitializeViewModel()
    {
        ViewModel = new MainViewModel();
        WindowBoundsService.Restore(this, ViewModel);
        _trayService = new TrayService(this, ViewModel);
        RootGrid.DataContext = ViewModel;
        StatusIndicators.DataContext = ViewModel;
        ViewModel.PropertyChanged += ViewModel_PropertyChanged;
        RootGrid.Loaded += MainWindow_Loaded;
        Activated += (_, _) => ViewModel.RefreshPacState();
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        WindowTitleBarHelper.ApplyTheme(this);
        ApplyTabVisibility();
        SyncNavigationFromViewModel();
        _trayService?.UpdateMenu();

        try
        {
            await ViewModel.InitializeAsync();
        }
        catch (Exception ex)
        {
            AppLog.Error(ex, "Ошибка инициализации приложения");
        }

        ApplyTabVisibility();
        SyncNavigationFromViewModel();
        _trayService?.UpdateMenu();

        if (ViewModel.StartMinimizedToTray)
        {
            _trayService?.MinimizeToTray(showNotification: false);
        }
    }

    public void StopServicesOnExit()
    {
        ViewModel.StopLocalProxyOnExit();
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.SelectedSection))
        {
            SyncNavigationFromViewModel();
        }
        else if (e.PropertyName is nameof(MainViewModel.IsTunnelingTabVisible)
                 or nameof(MainViewModel.IsProxyTabVisible)
                 or nameof(MainViewModel.IsProcessModeTabVisible)
                 or nameof(MainViewModel.IsBypassTabVisible))
        {
            ApplyTabVisibility();
        }
    }

    private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (_isSyncingNavigation || args.SelectedItem is not NavigationViewItem item)
        {
            return;
        }

        if (item.Tag is not string tag || !int.TryParse(tag, out var section))
        {
            return;
        }

        ViewModel.SelectedSection = section;
        NavigateToSection(section);
    }

    private void SyncNavigationFromViewModel()
    {
        _isSyncingNavigation = true;
        try
        {
            var item = ViewModel.SelectedSection switch
            {
                0 => TunnelingNavItem,
                1 => ProxyNavItem,
                2 => ProcessModeNavItem,
                3 => BypassNavItem,
                _ => SettingsNavItem
            };

            NavView.SelectedItem = item;
            NavigateToSection(ViewModel.SelectedSection);
        }
        finally
        {
            _isSyncingNavigation = false;
        }
    }

    private void NavigateToSection(int section)
    {
        var pageType = section switch
        {
            0 => typeof(TunnelingPage),
            1 => typeof(ProxyPage),
            2 => typeof(ProcessModePage),
            3 => typeof(BypassPage),
            _ => typeof(SettingsPage)
        };

        if (ContentFrame.CurrentSourcePageType != pageType)
        {
            ContentFrame.Navigate(pageType, ViewModel);
        }
    }

    private void ApplyTabVisibility()
    {
        TunnelingNavItem.Visibility = ViewModel.IsTunnelingTabVisible ? Visibility.Visible : Visibility.Collapsed;
        ProxyNavItem.Visibility = ViewModel.IsProxyTabVisible ? Visibility.Visible : Visibility.Collapsed;
        ProcessModeNavItem.Visibility = ViewModel.IsProcessModeTabVisible ? Visibility.Visible : Visibility.Collapsed;
        BypassNavItem.Visibility = ViewModel.IsBypassTabVisible ? Visibility.Visible : Visibility.Collapsed;
    }

}
