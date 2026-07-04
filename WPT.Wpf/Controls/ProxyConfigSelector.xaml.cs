using System.Collections;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using WPT.Wpf.ViewModels;

namespace WPT.Wpf.Controls;

public partial class ProxyConfigSelector
{
    public static readonly DependencyProperty ItemsSourceProperty =
        DependencyProperty.Register(
            nameof(ItemsSource),
            typeof(IEnumerable),
            typeof(ProxyConfigSelector),
            new PropertyMetadata(null));

    public static readonly DependencyProperty SelectedItemProperty =
        DependencyProperty.Register(
            nameof(SelectedItem),
            typeof(SavedProxyConfigItem),
            typeof(ProxyConfigSelector),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public static readonly DependencyProperty DeleteCommandProperty =
        DependencyProperty.Register(
            nameof(DeleteCommand),
            typeof(ICommand),
            typeof(ProxyConfigSelector),
            new PropertyMetadata(null));

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

    private void OnOpenDropDownClick(object sender, RoutedEventArgs e)
    {
        DropDownPopup.IsOpen = !DropDownPopup.IsOpen;

        if (DropDownPopup.IsOpen && SelectedItem != null)
        {
            DropDownList.SelectedItem = SelectedItem;
        }
    }

    private void OnDropDownSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.AddedItems.Count == 0 || DropDownPopup.IsOpen == false)
        {
            return;
        }

        DropDownPopup.IsOpen = false;
    }

    private void OnDropDownItemMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (IsActionClick(e.OriginalSource as DependencyObject))
        {
            return;
        }

        var listBoxItem = FindAncestor<ListBoxItem>(e.OriginalSource as DependencyObject);
        if (listBoxItem?.DataContext is not SavedProxyConfigItem item)
        {
            return;
        }

        SelectedItem = item;
        DropDownList.SelectedItem = item;
        DropDownPopup.IsOpen = false;
        e.Handled = true;
    }

    private void OnPingButtonMouseDown(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;

        if (ResolveConfigItem(sender) is SavedProxyConfigItem item)
        {
            _ = item.PingAsync();
        }
    }

    private void OnDeleteButtonMouseDown(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;

        if (ResolveConfigItem(sender) is not SavedProxyConfigItem item)
        {
            return;
        }

        if (DeleteCommand?.CanExecute(item) == true)
        {
            DeleteCommand.Execute(item);
        }
    }

    private static SavedProxyConfigItem? ResolveConfigItem(object sender)
    {
        if (sender is FrameworkElement { DataContext: SavedProxyConfigItem item })
        {
            return item;
        }

        if (sender is DependencyObject source)
        {
            var listBoxItem = FindAncestor<ListBoxItem>(source);
            if (listBoxItem?.DataContext is SavedProxyConfigItem fromRow)
            {
                return fromRow;
            }
        }

        return null;
    }

    private static bool IsActionClick(DependencyObject? source)
    {
        while (source != null)
        {
            if (source is System.Windows.Controls.Button or RepeatButton or ToggleButton)
            {
                return true;
            }

            source = VisualTreeHelper.GetParent(source);
        }

        return false;
    }

    private static T? FindAncestor<T>(DependencyObject? current) where T : DependencyObject
    {
        while (current != null)
        {
            if (current is T match)
            {
                return match;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return null;
    }

}
