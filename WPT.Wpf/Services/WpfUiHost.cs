using System.Windows.Threading;
using WPT.ViewModels.Abstractions;
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

    public MessageDialogResult ShowConfirm(string message, string title)
    {
        var dispatcher = WpfApplication.Current?.Dispatcher;
        if (dispatcher == null || dispatcher.CheckAccess())
        {
            return MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes
                ? MessageDialogResult.Yes
                : MessageDialogResult.No;
        }

        return dispatcher.Invoke(() =>
            MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes
                ? MessageDialogResult.Yes
                : MessageDialogResult.No);
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
