using WPT.Core.Services;
using WPT.Wpf.Controls;

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
        if (!IsElevatedLaunch(e.Args))
        {
            var settings = SettingsService.Load();
            if (settings.RunAsAdministrator && !AdminHelper.IsRunningAsAdmin())
            {
                if (!AdminHelper.TryRestartAsAdmin(ElevatedArg))
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

    private static bool IsElevatedLaunch(string[] args) =>
        args.Any(arg => arg.Equals(ElevatedArg, StringComparison.OrdinalIgnoreCase));
}
