using WPT.Wpf.Controls;

namespace WPT.Wpf;

public partial class App : System.Windows.Application
{
    static App()
    {
        ComboBoxAssist.EnsureInitialized();
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
}
