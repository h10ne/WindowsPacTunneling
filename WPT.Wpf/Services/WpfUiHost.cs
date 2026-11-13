using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using WPT.Core.Services;
using WPT.ViewModels.Abstractions;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using VerticalAlignment = System.Windows.VerticalAlignment;
using WpfControls = System.Windows.Controls;
using MessageBox = System.Windows.MessageBox;
using MessageBoxButton = System.Windows.MessageBoxButton;
using MessageBoxImage = System.Windows.MessageBoxImage;
using MessageBoxResult = System.Windows.MessageBoxResult;
using WpfApplication = System.Windows.Application;

namespace WPT.Wpf.Services;

public sealed class WpfUiHost : IUiHost
{
    public void RunOnUiThread(Action action)
    {
        var dispatcher = WpfApplication.Current?.Dispatcher;
        if (dispatcher == null || dispatcher.CheckAccess())
        {
            action();
            return;
        }

        dispatcher.BeginInvoke(action, DispatcherPriority.DataBind);
    }

    public Task RunOnUiThreadAsync(Action action)
    {
        var dispatcher = WpfApplication.Current?.Dispatcher;
        if (dispatcher == null || dispatcher.CheckAccess())
        {
            action();
            return Task.CompletedTask;
        }

        return dispatcher.InvokeAsync(action, DispatcherPriority.DataBind).Task;
    }

    public void ShowMessage(string message, string title, MessageDialogIcon icon = MessageDialogIcon.Information)
    {
        RunOnUiThread(() => MessageBox.Show(message, title, MessageBoxButton.OK, MapIcon(icon)));
    }

    public Task<MessageDialogResult> ShowConfirmAsync(string message, string title)
    {
        var dispatcher = WpfApplication.Current?.Dispatcher;
        if (dispatcher == null || dispatcher.CheckAccess())
        {
            return Task.FromResult(Confirm());
        }

        return dispatcher.InvokeAsync(Confirm).Task;

        MessageDialogResult Confirm() =>
            MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes
                ? MessageDialogResult.Yes
                : MessageDialogResult.No;
    }

    public void Shutdown(int exitCode = 0) =>
        RunOnUiThread(() => WpfApplication.Current.Shutdown(exitCode));

    public void SetClipboardText(string text) => ClipboardHelper.SetText(text);

    public IUiTimer CreateTimer(TimeSpan interval) => new WpfUiTimer(interval);

    public bool TryRestartElevated() => App.TryRestartElevated();

    public string? PickOpenFile(string title, string filter)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = title,
            Filter = filter,
            CheckFileExists = true,
            Multiselect = false
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public Task<IReadOnlyList<string>> PickRunningApplicationsAsync(IReadOnlyList<RunningApplication> applications)
    {
        var owner = WpfApplication.Current.MainWindow;
        var list = new WpfControls.ListBox
        {
            SelectionMode = WpfControls.SelectionMode.Multiple,
            MaxHeight = 420,
            Background = owner.Background,
            Foreground = owner.Foreground
        };

        foreach (var application in applications)
        {
            var row = new WpfControls.StackPanel { Orientation = WpfControls.Orientation.Horizontal };
            row.Children.Add(new WpfControls.Image
            {
                Width = 20,
                Height = 20,
                Margin = new Thickness(0, 0, 10, 0),
                Source = LoadExecutableIcon(application.Path)
            });
            row.Children.Add(new WpfControls.TextBlock { Text = application.FileName, VerticalAlignment = VerticalAlignment.Center });
            list.Items.Add(new WpfControls.ListBoxItem { Content = row, Tag = application.FileName, ToolTip = application.Path });
        }

        var add = new WpfControls.Button { Content = "Добавить", IsDefault = true, MinWidth = 90, Margin = new Thickness(0, 0, 8, 0), Style = (Style)WpfApplication.Current.FindResource("PrimaryButton") };
        var cancel = new WpfControls.Button { Content = "Отмена", IsCancel = true, MinWidth = 90, Style = (Style)WpfApplication.Current.FindResource("SecondaryButton") };
        var buttons = new WpfControls.StackPanel
        {
            Orientation = WpfControls.Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 12, 0, 0),
            Children = { add, cancel }
        };

        var content = new WpfControls.DockPanel { Margin = new Thickness(16) };
        WpfControls.DockPanel.SetDock(buttons, WpfControls.Dock.Bottom);
        content.Children.Add(buttons);
        content.Children.Add(list);

        var dialog = new Window
        {
            Title = "Запущенные приложения",
            Owner = owner,
            Width = 420,
            SizeToContent = SizeToContent.Height,
            ResizeMode = ResizeMode.NoResize,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            ShowInTaskbar = false,
            Background = owner.Background,
            Foreground = owner.Foreground,
            Content = content
        };
        add.Click += (_, _) => dialog.DialogResult = true;

        IReadOnlyList<string> selected = dialog.ShowDialog() == true
            ? list.SelectedItems.Cast<WpfControls.ListBoxItem>().Select(x => (string)x.Tag).ToList()
            : [];
        return Task.FromResult(selected);
    }

    private static BitmapSource? LoadExecutableIcon(string path)
    {
        try
        {
            using var icon = System.Drawing.Icon.ExtractAssociatedIcon(path);
            return icon == null
                ? null
                : System.Windows.Interop.Imaging.CreateBitmapSourceFromHIcon(icon.Handle, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
        }
        catch
        {
            // Нет доступа к файлу или у exe нет иконки — строка просто без картинки
            return null;
        }
    }

    private static MessageBoxImage MapIcon(MessageDialogIcon icon) => icon switch
    {
        MessageDialogIcon.Warning => MessageBoxImage.Warning,
        MessageDialogIcon.Error => MessageBoxImage.Error,
        MessageDialogIcon.Question => MessageBoxImage.Question,
        _ => MessageBoxImage.Information
    };

    private sealed class WpfUiTimer : IUiTimer
    {
        private readonly DispatcherTimer _timer;

        public WpfUiTimer(TimeSpan interval)
        {
            _timer = new DispatcherTimer { Interval = interval };
            _timer.Tick += (_, _) => Tick?.Invoke(this, EventArgs.Empty);
        }

        public event EventHandler? Tick;

        public TimeSpan Interval
        {
            get => _timer.Interval;
            set => _timer.Interval = value;
        }

        public void Start() => _timer.Start();

        public void Stop() => _timer.Stop();

        public void Dispose() => _timer.Stop();
    }

}
