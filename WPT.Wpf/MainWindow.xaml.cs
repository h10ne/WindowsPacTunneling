using System.IO;

using System.Windows;

using System.Windows.Interop;

using System.Windows.Media.Imaging;

using WPT.Wpf.Services;

using WPT.Wpf.Ui;

using WPT.Wpf.ViewModels;



namespace WPT.Wpf;



public partial class MainWindow : Window

{

    private readonly MainViewModel _viewModel;

    private readonly TrayService _trayService;



    public MainWindow()

    {

        InitializeComponent();
        TrySetWindowIcon(this);

        _viewModel = new MainViewModel();

        DataContext = _viewModel;

        _trayService = new TrayService(this, _viewModel);

        SourceInitialized += (_, _) => NativeTheme.ApplyDarkTitleBar(this);

        Loaded += MainWindow_Loaded;

        Activated += (_, _) => _viewModel.RefreshPacState();

    }



    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)

    {

        NativeTheme.ApplyDarkTitleBar(this);

        await _viewModel.InitializeAsync();

        _trayService.UpdateMenu();

        if (_viewModel.StartMinimizedToTray)
        {
            _trayService.MinimizeToTray(showNotification: false);
        }

    }

    public void StopLocalProxyOnExit() => _viewModel.StopLocalProxyOnExit();

    private static void TrySetWindowIcon(Window window)
    {
        try
        {
            var iconPath = Path.Combine(AppContext.BaseDirectory, "Resources", "app.ico");

            if (File.Exists(iconPath))
            {
                window.Icon = BitmapFrame.Create(new Uri(iconPath, UriKind.Absolute));
            }
        }
        catch
        {
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        _viewModel.StopLocalProxyOnExit();
        _trayService.Dispose();
        base.OnClosed(e);
    }

}

