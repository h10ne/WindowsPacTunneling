using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.ApplicationModel.DataTransfer;
using WinRT.Interop;
using WPT.Core.Services;
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

        // WinRT FileOpenPicker под администратором падает с E_FAIL, а WPT обычно запущен от администратора.
        // Win32-диалог работает в любом режиме и модален синхронно
        string? result = null;
        RunOnUiThreadAsync(() =>
        {
            using var dialog = new System.Windows.Forms.OpenFileDialog
            {
                Title = title,
                Filter = filter,
                CheckFileExists = true
            };

            if (dialog.ShowDialog(new Win32Owner(WindowNative.GetWindowHandle(_window))) == System.Windows.Forms.DialogResult.OK)
            {
                result = dialog.FileName;
            }
        }).GetAwaiter().GetResult();

        return result;
    }

    private sealed class Win32Owner(IntPtr handle) : System.Windows.Forms.IWin32Window
    {
        public IntPtr Handle => handle;
    }

    public async Task<IReadOnlyList<string>> PickRunningApplicationsAsync(IReadOnlyList<RunningApplication> applications)
    {
        if (_window?.Content is not FrameworkElement root)
        {
            return [];
        }

        var list = new ListView
        {
            SelectionMode = ListViewSelectionMode.Multiple,
            MaxHeight = 420,
            MinWidth = 360
        };

        foreach (var application in applications)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
            row.Children.Add(new Image { Width = 20, Height = 20, Source = LoadExecutableIcon(application.Path) });
            row.Children.Add(new TextBlock { Text = application.FileName, VerticalAlignment = VerticalAlignment.Center });

            var item = new ListViewItem { Content = row, Tag = application.FileName };
            ToolTipService.SetToolTip(item, application.Path);
            list.Items.Add(item);
        }

        var dialog = new ContentDialog
        {
            Title = "Запущенные приложения",
            Content = list,
            PrimaryButtonText = "Добавить",
            CloseButtonText = "Отмена",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = root.XamlRoot
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
        {
            return [];
        }

        return list.SelectedItems.OfType<ListViewItem>().Select(x => (string)x.Tag).ToList();
    }

    private static BitmapImage? LoadExecutableIcon(string path)
    {
        try
        {
            using var icon = System.Drawing.Icon.ExtractAssociatedIcon(path);
            if (icon == null)
            {
                return null;
            }

            using var bitmap = icon.ToBitmap();
            using var stream = new MemoryStream();
            bitmap.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
            stream.Position = 0;

            var image = new BitmapImage();
            image.SetSource(stream.AsRandomAccessStream());
            return image;
        }
        catch
        {
            // Нет доступа к файлу или у exe нет иконки — строка просто без картинки
            return null;
        }
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
