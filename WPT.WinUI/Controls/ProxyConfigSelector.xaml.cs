using System.Collections;
using System.Windows.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WPT.ViewModels;

namespace WPT.WinUI.Controls;

public sealed partial class ProxyConfigSelector : UserControl
{
    public static readonly DependencyProperty ItemsSourceProperty =
        DependencyProperty.Register(nameof(ItemsSource), typeof(IEnumerable), typeof(ProxyConfigSelector), new PropertyMetadata(null));

    public static readonly DependencyProperty SelectedItemProperty =
        DependencyProperty.Register(nameof(SelectedItem), typeof(SavedProxyConfigItem), typeof(ProxyConfigSelector), new PropertyMetadata(null));

    public static readonly DependencyProperty DeleteCommandProperty =
        DependencyProperty.Register(nameof(DeleteCommand), typeof(ICommand), typeof(ProxyConfigSelector), new PropertyMetadata(null));

    public static readonly DependencyProperty EditingEnabledProperty =
        DependencyProperty.Register(nameof(EditingEnabled), typeof(bool), typeof(ProxyConfigSelector), new PropertyMetadata(true));

    public ProxyConfigSelector()
    {
        InitializeComponent();
    }

    public IEnumerable? ItemsSource
    {
        get => (IEnumerable?)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    public SavedProxyConfigItem? SelectedItem
    {
        get => (SavedProxyConfigItem?)GetValue(SelectedItemProperty);
        set => SetValue(SelectedItemProperty, value);
    }

    public ICommand? DeleteCommand
    {
        get => (ICommand?)GetValue(DeleteCommandProperty);
        set => SetValue(DeleteCommandProperty, value);
    }

    public bool EditingEnabled
    {
        get => (bool)GetValue(EditingEnabledProperty);
        set => SetValue(EditingEnabledProperty, value);
    }

    private void OnPingClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: SavedProxyConfigItem item })
        {
            _ = item.PingAsync();
        }
    }

    private void OnDeleteItemClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: SavedProxyConfigItem item })
        {
            DeleteItem(item);
        }
    }

    private void DeleteItem(SavedProxyConfigItem? item)
    {
        if (item == null)
        {
            return;
        }

        if (DeleteCommand?.CanExecute(item) == true)
        {
            DeleteCommand.Execute(item);
        }
    }

}
