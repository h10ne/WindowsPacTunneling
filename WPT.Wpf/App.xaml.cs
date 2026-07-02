using System.Windows;
using WPT.Core.Services;
using WPT.Wpf.Controls;
using WPT.Wpf.Services;

namespace WPT.Wpf;

public partial class App : System.Windows.Application
{
    private const string ElevatedArg = "--elevated";

    static App()
    {
        ComboBoxAssist.EnsureInitialized();
    }

    protected override void OnStartup(System.Windows.StartupEventArgs e)
    {
        if (!SingleInstanceGuard.TryAcquire())
        {
            System.Windows.MessageBox.Show(
                "Приложение WPT уже запущено. Используйте окно или иконку в системном трее.",
                "WPT",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            Shutdown(0);
            return;
        }

        AppLog.Initialize();

        if (!IsElevatedLaunch(e.Args))
        {
            var settings = SettingsService.Load();
            if (settings.RunAsAdministrator && !AdminHelper.IsRunningAsAdmin())
            {
                if (!TryRestartElevated())
                {
                    Shutdown(0);
                    return;
                }
            }
        }

        base.OnStartup(e);
    }

    protected override void OnExit(System.Windows.ExitEventArgs e)
    {
        if (MainWindow is MainWindow mainWindow)
        {
            mainWindow.StopLocalProxyOnExit();
        }

        AppLog.Close();
        SingleInstanceGuard.Release();
        base.OnExit(e);
    }

    protected override void OnSessionEnding(System.Windows.SessionEndingCancelEventArgs e)
    {
        if (MainWindow is MainWindow mainWindow)
        {
            mainWindow.StopLocalProxyOnExit();
        }

        base.OnSessionEnding(e);
    }

    internal static bool TryRestartElevated()
    {
        SingleInstanceGuard.Release();

        if (!AdminHelper.TryRestartAsAdmin(ElevatedArg))
        {
            return false;
        }

        if (!SingleInstanceGuard.TryAcquire())
        {
            return false;
        }

        return true;
    }

    private static bool IsElevatedLaunch(string[] args) =>
        args.Any(arg => arg.Equals(ElevatedArg, StringComparison.OrdinalIgnoreCase));
}
