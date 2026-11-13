using WPT.Core.Services;

namespace WPT.ViewModels.Abstractions;

public enum MessageDialogIcon
{
    Information,
    Warning,
    Error,
    Question
}

public enum MessageDialogResult
{
    Ok,
    Yes,
    No,
    Cancel
}

public interface IUiTimer : IDisposable
{
    event EventHandler? Tick;

    TimeSpan Interval { get; set; }

    void Start();

    void Stop();
}

public interface IUiHost
{
    void RunOnUiThread(Action action);

    Task RunOnUiThreadAsync(Action action);

    void ShowMessage(string message, string title, MessageDialogIcon icon = MessageDialogIcon.Information);

    Task<MessageDialogResult> ShowConfirmAsync(string message, string title);

    void Shutdown(int exitCode = 0);

    void SetClipboardText(string text);

    IUiTimer CreateTimer(TimeSpan interval);

    bool TryRestartElevated();

    string? PickOpenFile(string title, string filter);

    /// <summary> Диалог выбора запущенных приложений с иконками. Вызывать из UI-потока </summary>
    /// <param name="applications"> Кандидаты для выбора </param>
    /// <returns> Имена выбранных exe, пусто при отмене </returns>
    Task<IReadOnlyList<string>> PickRunningApplicationsAsync(IReadOnlyList<RunningApplication> applications);
}

public static class UiHostContext
{
    private static IUiHost? _current;

    public static IUiHost Current =>
        _current ?? throw new InvalidOperationException("UiHostContext не инициализирован. Вызовите UiHostContext.Initialize() при старте приложения.");

    public static void Initialize(IUiHost host) => _current = host;
}
