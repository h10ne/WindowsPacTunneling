using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage.Pickers;
using WinRT.Interop;
using WPT.ViewModels.Abstractions;

namespace WPT.WinUI.Services;

public sealed class WinUiHost : IUiHost
{
    private Window? _window;
    private DispatcherQueue? _dispatcher;

    public void SetWindow(Window window)
    {
        _window = window;
        _dispatcher = window.DispatcherQueue;
    }

    public void RunOnUiThread(Action action)
    {
        var dispatcher = ResolveDispatcher();
        if (dispatcher.HasThreadAccess)
        {
            action();
            return;
        }

        _ = dispatcher.TryEnqueue(() => action());
    }

    public Task RunOnUiThreadAsync(Action action)
    {
        var dispatcher = ResolveDispatcher();
        if (dispatcher.HasThreadAccess)
        {
            action();
            return Task.CompletedTask;
        }

        var tcs = new TaskCompletionSource();
        _ = dispatcher.TryEnqueue(() =>
        {
            try
            {
                action();
                tcs.SetResult();
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        });

        return tcs.Task;
    }

    public void ShowMessage(string message, string title, MessageDialogIcon icon = MessageDialogIcon.Information)
    {
        RunOnUiThread(() => _ = ShowDialogAsync(title, message, confirm: false));
    }

    public MessageDialogResult ShowConfirm(string message, string title)
    {
        MessageDialogResult result = MessageDialogResult.No;
        RunOnUiThreadAsync(async () =>
        {
            result = await ShowDialogAsync(title, message, confirm: true);
        }).GetAwaiter().GetResult();

        return result;
    }

    public void Shutdown(int exitCode = 0) =>
        RunOnUiThread(() => Application.Current.Exit());

    public void SetClipboardText(string text)
    {
        var data = new DataPackage();
        data.SetText(text);
        Clipboard.SetContent(data);
    }

    public IUiTimer CreateTimer(TimeSpan interval) => new WinUiTimer(ResolveDispatcher(), interval);

    public bool TryRestartElevated() => App.TryRestartElevated();

    public string? PickOpenFile(string title, string filter)
    {
        if (_window == null)
        {
            return null;
        }

        string? result = null;
        RunOnUiThreadAsync(async () =>
        {
            var picker = new FileOpenPicker
            {
                SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
                ViewMode = PickerViewMode.List
            };

            foreach (var part in filter.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (part.StartsWith('*'))
                {
                    picker.FileTypeFilter.Add(part.TrimStart('*'));
                }
            }

            if (picker.FileTypeFilter.Count == 0)
            {
                picker.FileTypeFilter.Add(".conf");
            }

            InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(_window));
            var file = await picker.PickSingleFileAsync();
            result = file?.Path;
        }).GetAwaiter().GetResult();

        return result;
    }

    private DispatcherQueue ResolveDispatcher() =>
        _dispatcher ?? DispatcherQueue.GetForCurrentThread()
        ?? throw new InvalidOperationException("DispatcherQueue не инициализирован.");

    private async Task<MessageDialogResult> ShowDialogAsync(string title, string content, bool confirm)
    {
        if (_window?.Content is not FrameworkElement root)
        {
            return MessageDialogResult.Cancel;
        }

        var dialog = new ContentDialog
        {
            Title = title,
            Content = new TextBlock
            {
                Text = content,
                TextWrapping = TextWrapping.WrapWholeWords,
                MaxWidth = 420
            },
            XamlRoot = root.XamlRoot,
            DefaultButton = confirm ? ContentDialogButton.Primary : ContentDialogButton.Close
        };

        if (confirm)
        {
            dialog.PrimaryButtonText = "Да";
            dialog.CloseButtonText = "Нет";
        }
        else
        {
            dialog.CloseButtonText = "OK";
        }

        var result = await dialog.ShowAsync();
        if (!confirm)
        {
            return MessageDialogResult.Ok;
        }

        return result == ContentDialogResult.Primary ? MessageDialogResult.Yes : MessageDialogResult.No;
    }

    private sealed class WinUiTimer : IUiTimer
    {
        private readonly DispatcherQueueTimer _timer;

        public WinUiTimer(DispatcherQueue dispatcher, TimeSpan interval)
        {
            _timer = dispatcher.CreateTimer();
            _timer.Interval = interval;
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
