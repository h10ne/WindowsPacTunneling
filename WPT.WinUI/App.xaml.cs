using System.Diagnostics;
using Microsoft.UI.Xaml;
using WPT.Core.Services;
using WPT.ViewModels;
using WPT.ViewModels.Abstractions;
using WPT.WinUI.Services;

namespace WPT.WinUI;

public partial class App : Application
{
    private const string ElevatedArg = "--elevated";

    private Window? _window;

    public static WinUiHost UiHost { get; } = new();

    public App()
    {
        Environment.SetEnvironmentVariable("MICROSOFT_WINDOWSAPPRUNTIME_BASE_DIRECTORY", AppContext.BaseDirectory);
        InitializeComponent();
        RegisterGlobalExceptionHandlers();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        if (!SingleInstanceGuard.TryAcquire())
        {
            AppLog.Initialize();
            AppLog.Warning("Попытка запуска второго экземпляра WPT.WinUI");
            Exit();
            return;
        }

        AppLog.Initialize();

        if (!IsElevatedLaunch(args.Arguments) && ShouldRestartElevated())
        {
            if (!TryRestartElevated())
            {
                Exit();
                return;
            }
        }

        UiHostContext.Initialize(UiHost);
        var window = new MainWindow();
        _window = window;
        UiHost.SetWindow(window);
        window.InitializeViewModel();
        window.Activate();
    }

    internal static bool TryRestartElevated()
    {
        SingleInstanceGuard.Release();

        if (!AdminHelper.TryRestartAsAdmin(ElevatedArg))
        {
            return false;
        }

        return SingleInstanceGuard.TryAcquire();
    }

    private static bool ShouldRestartElevated()
    {
        if (Debugger.IsAttached)
        {
            return false;
        }

        var settings = SettingsService.Load();
        return settings.RunAsAdministrator && !AdminHelper.IsRunningAsAdmin();
    }

    private static bool IsElevatedLaunch(string args) =>
        args.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Any(arg => arg.Equals(ElevatedArg, StringComparison.OrdinalIgnoreCase));

    private void RegisterGlobalExceptionHandlers()
    {
        UnhandledException += (_, e) =>
        {
            AppLog.Initialize();
            AppLog.Error(e.Exception, "Необработанное исключение UI-потока WinUI");
            e.Handled = true;
        };

        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            if (e.ExceptionObject is Exception ex)
            {
                AppLog.Initialize();
                AppLog.Error(ex, "Необработанное исключение приложения WinUI");
            }
        };

        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            AppLog.Initialize();
            AppLog.Error(e.Exception, "Необработанное исключение фоновой задачи WinUI");
            e.SetObserved();
        };
    }

}
