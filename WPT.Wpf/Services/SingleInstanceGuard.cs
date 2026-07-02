namespace WPT.Wpf.Services;

public static class SingleInstanceGuard
{
    private const string MutexName = "Global\\WPT.WindowPacTunneling.SingleInstance";

    private static Mutex? _mutex;

    public static bool TryAcquire()
    {
        _mutex = new Mutex(true, MutexName, out var createdNew);
        if (createdNew)
        {
            return true;
        }

        _mutex.Dispose();
        _mutex = null;
        return false;
    }

    public static void Release()
    {
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
}
