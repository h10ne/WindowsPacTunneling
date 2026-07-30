using System.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;
using WPT.ViewModels;

namespace WPT.WinUI.Controls;

public sealed partial class StatusIndicatorsControl : UserControl
{
    private MainViewModel? _viewModel;
    private NavigationView? _navView;
    private bool _navViewEventsAttached;
    private bool _parentEventsAttached;
    private bool _compactRefreshScheduled;
    private double _lastFooterWidth;

    public StatusIndicatorsControl()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => BindViewModel();
        Loaded += StatusRoot_Loaded;
        UpdateText.PointerPressed += UpdateText_PointerPressed;
        UpdateHost.SizeChanged += UpdateHost_SizeChanged;
    }

    private void UpdateHost_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        ApplyUpdatePresentation(IsCompactLayout());
    }

    private void StatusRoot_Loaded(object sender, RoutedEventArgs e)
    {
        AttachNavigationViewEvents();
        AttachParentEvents();
        ScheduleCompactRefresh();
    }

    private void StatusRoot_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (e.NewSize.Width > 0)
        {
            _lastFooterWidth = e.NewSize.Width;
        }

        ApplyCompactLayout();
    }

    private void AttachParentEvents()
    {
        if (_parentEventsAttached || Parent is not FrameworkElement host)
        {
            return;
        }

        _parentEventsAttached = true;
        host.SizeChanged += ParentHost_SizeChanged;
    }

    private void ParentHost_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (e.NewSize.Width > 0)
        {
            _lastFooterWidth = e.NewSize.Width;
        }

        ApplyCompactLayout();
    }

    private void AttachNavigationViewEvents()
    {
        if (_navViewEventsAttached)
        {
            return;
        }

        _navView = FindAncestor<NavigationView>(this);
        if (_navView is null)
        {
            return;
        }

        _navViewEventsAttached = true;
        _navView.DisplayModeChanged += (_, _) => ScheduleCompactRefresh();
        _navView.RegisterPropertyChangedCallback(NavigationView.IsPaneOpenProperty, (_, _) => ScheduleCompactRefresh());
    }

    private void ScheduleCompactRefresh()
    {
        if (_compactRefreshScheduled)
        {
            return;
        }

        _compactRefreshScheduled = true;
        DispatcherQueue.TryEnqueue(() =>
        {
            _compactRefreshScheduled = false;
            ApplyCompactLayout();
        });

        _ = RunDelayedCompactRefresh();
    }

    private async Task RunDelayedCompactRefresh()
    {
        await Task.Delay(50);
        DispatcherQueue.TryEnqueue(ApplyCompactLayout);
        await Task.Delay(200);
        DispatcherQueue.TryEnqueue(ApplyCompactLayout);
    }

    private bool IsCompactLayout()
    {
        if (_navView is not null)
        {
            if (!_navView.IsPaneOpen)
            {
                return false;
            }

            if (_navView.DisplayMode is NavigationViewDisplayMode.Compact or NavigationViewDisplayMode.Minimal)
            {
                return true;
            }

            var contentLeft = GetContentFrameLeftOffset(_navView);
            if (contentLeft > 0)
            {
                return contentLeft <= _navView.CompactPaneLength + 8;
            }
        }

        var width = GetFooterWidth();
        if (width <= 0)
        {
            return false;
        }

        var compactLimit = (_navView?.CompactPaneLength ?? 48) + 16;
        return width <= compactLimit;
    }

    private static double GetContentFrameLeftOffset(NavigationView navView)
    {
        var frame = FindContentFrame(navView);
        if (frame is null)
        {
            return -1;
        }

        try
        {
            return frame.TransformToVisual(navView).TransformPoint(new Point(0, 0)).X;
        }
        catch
        {
            return -1;
        }

    }

    private static Frame? FindContentFrame(NavigationView navView)
    {
        return FindDescendant<Frame>(navView);
    }

    private static T? FindDescendant<T>(DependencyObject root) where T : DependencyObject
    {
        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T match)
            {
                return match;
            }

            var nested = FindDescendant<T>(child);
            if (nested is not null)
            {
                return nested;
            }
        }

        return null;
    }

    private double GetFooterWidth()
    {
        if (_lastFooterWidth > 0)
        {
            return _lastFooterWidth;
        }

        if (Parent is FrameworkElement host && host.ActualWidth > 0)
        {
            return host.ActualWidth;
        }

        if (ActualWidth > 0)
        {
            return ActualWidth;
        }

        return RenderSize.Width;
    }

    private static T? FindAncestor<T>(DependencyObject start) where T : DependencyObject
    {
        var current = VisualTreeHelper.GetParent(start);
        while (current is not null)
        {
            if (current is T match)
            {
                return match;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return null;
    }

    private void BindViewModel()
    {
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged -= ViewModel_PropertyChanged;
        }

        _viewModel = DataContext as MainViewModel;
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged += ViewModel_PropertyChanged;
        }

        UpdateTooltips();
        ApplyCompactLayout();
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(MainViewModel.IsAnyUpdateAvailable):
                ApplyUpdatePresentation();
                break;
            case nameof(MainViewModel.FooterRight):
            case nameof(MainViewModel.FooterProxyStatus):
            case nameof(MainViewModel.FooterProcessModeStatus):
            case nameof(MainViewModel.FooterBypassStatus):
                UpdateTooltips();
                break;
        }

    }

    private void UpdateText_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        OpenSettingsForUpdate();
    }

    private void UpdateIcon_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        OpenSettingsForUpdate();
    }

    private void OpenSettingsForUpdate()
    {
        if (_viewModel?.OpenSettingsForUpdateCommand.CanExecute(null) == true)
        {
            _viewModel.OpenSettingsForUpdateCommand.Execute(null);
        }

    }

    private void UpdateTooltips()
    {
        ToolTipService.SetToolTip(TunnelingDotHost, _viewModel?.FooterRight);
        ToolTipService.SetToolTip(ProxyDotHost, _viewModel?.FooterProxyStatus);
        ToolTipService.SetToolTip(ProcessModeDotHost, _viewModel?.FooterProcessModeStatus);
        ToolTipService.SetToolTip(BypassDotHost, _viewModel?.FooterBypassStatus);
    }

    private void ApplyCompactLayout()
    {
        AttachNavigationViewEvents();
        AttachParentEvents();

        var compact = IsCompactLayout();
        var rowSpacing = compact ? 2d : 8d;
        var textVisibility = compact ? Visibility.Collapsed : Visibility.Visible;

        HorizontalAlignment = HorizontalAlignment.Stretch;
        HorizontalContentAlignment = compact ? HorizontalAlignment.Center : HorizontalAlignment.Stretch;
        RootGrid.HorizontalAlignment = HorizontalAlignment.Stretch;
        RootPanel.HorizontalAlignment = compact ? HorizontalAlignment.Center : HorizontalAlignment.Stretch;
        Margin = compact ? new Thickness(0, 0, 0, 16) : new Thickness(12, 0, 12, 16);

        SetRowLayout(TunnelingRow, rowSpacing, compact);
        SetRowLayout(ProxyRow, rowSpacing, compact);
        SetRowLayout(ProcessModeRow, rowSpacing, compact);
        SetRowLayout(BypassRow, rowSpacing, compact);

        TunnelingStatusText.Visibility = textVisibility;
        ProxyStatusText.Visibility = textVisibility;
        ProcessModeStatusText.Visibility = textVisibility;
        BypassStatusText.Visibility = textVisibility;

        UpdateHost.HorizontalAlignment = compact ? HorizontalAlignment.Center : HorizontalAlignment.Stretch;
        UpdateHost.Margin = compact ? new Thickness(0, 2, 0, 0) : new Thickness(0, 6, 0, 0);
        UpdateIcon.Margin = compact ? new Thickness(-6, 0, 0, 0) : new Thickness(0);

        ApplyUpdatePresentation(compact);
    }

    private static void SetRowLayout(StackPanel row, double spacing, bool compact)
    {
        row.Spacing = spacing;
        row.HorizontalAlignment = compact ? HorizontalAlignment.Center : HorizontalAlignment.Left;
    }

    private void ApplyUpdatePresentation()
    {
        ApplyUpdatePresentation(IsCompactLayout());
    }

    private bool ShouldShowUpdateIcon(bool compact)
    {
        if (compact)
        {
            return true;
        }

        var hostWidth = UpdateHost.ActualWidth;
        if (hostWidth > 0 && hostWidth <= 100)
        {
            return true;
        }

        if (hostWidth > 0 && UpdateText.ActualWidth > hostWidth)
        {
            return true;
        }

        return false;
    }

    private void ApplyUpdatePresentation(bool compact)
    {
        UpdateText.Visibility = Visibility.Collapsed;
        UpdateIcon.Visibility = Visibility.Collapsed;

        if (_viewModel?.IsAnyUpdateAvailable != true)
        {
            return;
        }

        if (ShouldShowUpdateIcon(compact))
        {
            UpdateIcon.Visibility = Visibility.Visible;
            return;
        }

        UpdateText.Visibility = Visibility.Visible;
    }

}
