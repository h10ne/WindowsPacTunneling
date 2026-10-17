using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;

namespace WPT.Core.Services;

/// <summary>
/// Один экземпляр WPT (WinUI или WPF). Mutex и событие активации открыты всем
/// пользователям: иначе обычный процесс не может открыть объекты, созданные
/// elevated-экземпляром, и падал с UnauthorizedAccessException.
/// </summary>
public static class SingleInstanceGuard
{
    private const string MutexName = "Global\\WPT.WindowPacTunneling.SingleInstance";
    private const string ActivateEventName = "Global\\WPT.WindowPacTunneling.Activate";

    private static readonly SecurityIdentifier AuthenticatedUsers =
        new(WellKnownSidType.AuthenticatedUserSid, null);

    private static Mutex? _mutex;
    private static EventWaitHandle? _activateEvent;
    private static RegisteredWaitHandle? _activateWait;

    /// <summary>
    /// true — мы единственный экземпляр. false — WPT уже запущен; его окно попросят показаться.
    /// </summary>
    public static bool TryAcquire()
    {
        var security = new MutexSecurity();
        security.AddAccessRule(new MutexAccessRule(AuthenticatedUsers, MutexRights.FullControl, AccessControlType.Allow));

        try
        {
            var mutex = MutexAcl.Create(true, MutexName, out var createdNew, security);
            if (createdNew)
            {
                _mutex = mutex;
                return true;
            }

            mutex.Dispose();
        }
        catch (UnauthorizedAccessException)
        {
            // Mutex создан старой версией WPT от администратора — экземпляр точно есть.
        }

        SignalExistingInstance();
        return false;
    }

    /// <summary>Вызывать после создания окна: второй запуск WPT вызовет <paramref name="onActivate"/> (из пула потоков).</summary>
    public static void ListenForActivation(Action onActivate)
    {
        if (_mutex == null || _activateEvent != null)
        {
            return;
        }

        var security = new EventWaitHandleSecurity();
        security.AddAccessRule(new EventWaitHandleAccessRule(AuthenticatedUsers, EventWaitHandleRights.FullControl, AccessControlType.Allow));

        try
        {
            _activateEvent = EventWaitHandleAcl.Create(false, EventResetMode.AutoReset, ActivateEventName, out _, security);
            _activateWait = ThreadPool.RegisterWaitForSingleObject(
                _activateEvent, (_, _) => onActivate(), null, Timeout.Infinite, executeOnlyOnce: false);
        }
        catch (Exception ex)
        {
            AppLog.Warning($"Не удалось подписаться на активацию окна: {ex.Message}");
        }
    }

    public static void Release()
    {
        _activateWait?.Unregister(null);
        _activateWait = null;
        _activateEvent?.Dispose();
        _activateEvent = null;

        if (_mutex == null)
        {
            return;
        }

        try
        {
            _mutex.ReleaseMutex();
        }
        catch
        {
        }

        _mutex.Dispose();
        _mutex = null;
    }

    private static void SignalExistingInstance()
    {
        try
        {
            if (!EventWaitHandle.TryOpenExisting(ActivateEventName, out var activateEvent))
            {
                return;
            }

            using (activateEvent)
            {
                // Мы foreground-процесс (запуск пользователем) — разрешаем первому экземпляру вывести окно вперёд.
                AllowSetForegroundWindow(AsfwAny);
                activateEvent.Set();
            }
        }
        catch (Exception)
        {
            // Нет доступа или экземпляр завершается — просто выходим.
        }
    }

    private const int AsfwAny = -1;

    [DllImport("user32.dll")]
    private static extern bool AllowSetForegroundWindow(int dwProcessId);
}
