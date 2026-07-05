using System.Drawing;
using System.Windows;
using System.Windows.Forms;
using WPT.Core;
using WPT.Wpf.ViewModels;
using Application = System.Windows.Application;

namespace WPT.Wpf.Services;

public sealed class TrayService : IDisposable
{
    private readonly MainViewModel _viewModel;
    private readonly Window _window;
    private readonly NotifyIcon _notifyIcon;
    private readonly ToolStripMenuItem _enableItem;
    private readonly ToolStripMenuItem _disableItem;
    private readonly ToolStripMenuItem _proxyStartItem;
    private readonly ToolStripMenuItem _proxyStopItem;
    private readonly ToolStripMenuItem? _processModeStartItem;
    private readonly ToolStripMenuItem? _processModeStopItem;
    private readonly ToolStripMenuItem _zapretStartItem;
    private readonly ToolStripMenuItem _zapretStopItem;
    private bool _allowClose;

    public TrayService(Window window, MainViewModel viewModel)
    {
        _window = window;
        _viewModel = viewModel;

        Icon? icon = AppIcon.CreateTrayIcon();

        _notifyIcon = new NotifyIcon
        {
            Icon = icon ?? SystemIcons.Shield,
            Text = AppBranding.DisplayName,
            Visible = false
        };

        var menu = new ContextMenuStrip();
        menu.Items.Add("Открыть", null, (_, _) => ShowMainWindow());

        var proxyMenu = new ToolStripMenuItem("PAC");
        _enableItem = new ToolStripMenuItem("Включить", null, async (_, _) =>
        {
            ShowMainWindow();
            await _viewModel.ApplyFromTrayAsync();
            UpdateMenu();
        });
        _disableItem = new ToolStripMenuItem("Отключить", null, (_, _) =>
        {
            _viewModel.DisablePacCommand.Execute(null);
            UpdateMenu();
        });
        proxyMenu.DropDownItems.Add(_enableItem);
        proxyMenu.DropDownItems.Add(_disableItem);
        menu.Items.Add(proxyMenu);

        var localProxyMenu = new ToolStripMenuItem("Локальный прокси");
        _proxyStartItem = new ToolStripMenuItem("Запустить", null, (_, _) =>
        {
            if (_viewModel.IsProxyRunning || _viewModel.IsBusy || !_viewModel.IsTrayProxyMenuEnabled)
            {
                return;
            }

            _viewModel.ToggleProxyCommand.Execute(null);
            UpdateMenu();
        });
        _proxyStopItem = new ToolStripMenuItem("Остановить", null, (_, _) =>
        {
            if (!_viewModel.IsProxyRunning || _viewModel.IsBusy || !_viewModel.IsTrayProxyMenuEnabled)
            {
                return;
            }

            _viewModel.ToggleProxyCommand.Execute(null);
            UpdateMenu();
        });
        localProxyMenu.DropDownItems.Add(_proxyStartItem);
        localProxyMenu.DropDownItems.Add(_proxyStopItem);
        menu.Items.Add(localProxyMenu);

        if (_viewModel.IsProcessModeUiVisible)
        {
            var processModeMenu = new ToolStripMenuItem("Process Mode");
            _processModeStartItem = new ToolStripMenuItem("Запустить", null, (_, _) =>
            {
                if (_viewModel.IsProcessModeRunning
                    || _viewModel.IsProcessModeOperating
                    || !_viewModel.IsTrayProcessModeMenuEnabled)
                {
                    return;
                }

                _viewModel.ToggleProcessModeCommand.Execute(null);
                UpdateMenu();
            });
            _processModeStopItem = new ToolStripMenuItem("Остановить", null, (_, _) =>
            {
                if (!_viewModel.IsProcessModeRunning
                    || _viewModel.IsProcessModeOperating
                    || !_viewModel.IsTrayProcessModeMenuEnabled)
                {
                    return;
                }

                _viewModel.ToggleProcessModeCommand.Execute(null);
                UpdateMenu();
            });
            processModeMenu.DropDownItems.Add(_processModeStartItem);
            processModeMenu.DropDownItems.Add(_processModeStopItem);
            menu.Items.Add(processModeMenu);
        }

        var zapretMenu = new ToolStripMenuItem("Zapret");
        _zapretStartItem = new ToolStripMenuItem("Запустить", null, (_, _) =>
        {
            if (_viewModel.IsBypassRunning
                || _viewModel.IsBusy
                || _viewModel.IsBypassProbingStrategy
                || !_viewModel.IsTrayZapretMenuEnabled)
            {
                return;
            }

            _viewModel.ToggleBypassCommand.Execute(null);
            UpdateMenu();
        });
        _zapretStopItem = new ToolStripMenuItem("Остановить", null, (_, _) =>
        {
            if (!_viewModel.IsBypassRunning
                || _viewModel.IsBusy
                || _viewModel.IsBypassProbingStrategy
                || !_viewModel.IsTrayZapretMenuEnabled)
            {
                return;
            }

            _viewModel.ToggleBypassCommand.Execute(null);
            UpdateMenu();
        });
        zapretMenu.DropDownItems.Add(_zapretStartItem);
        zapretMenu.DropDownItems.Add(_zapretStopItem);
        menu.Items.Add(zapretMenu);

        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Выход", null, (_, _) => ExitApplication());

        _notifyIcon.ContextMenuStrip = menu;
        _notifyIcon.DoubleClick += (_, _) => ShowMainWindow();

        _window.Closing += Window_Closing;
        viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(MainViewModel.IsPacActive)
                or nameof(MainViewModel.IsProxyRunning)
                or nameof(MainViewModel.IsBusy)
                or nameof(MainViewModel.IsTrayProxyMenuEnabled)
                or nameof(MainViewModel.IsProcessModeRunning)
                or nameof(MainViewModel.IsProcessModeOperating)
                or nameof(MainViewModel.IsTrayProcessModeMenuEnabled)
                or nameof(MainViewModel.IsBypassRunning)
                or nameof(MainViewModel.IsBypassProbingStrategy)
                or nameof(MainViewModel.IsTrayZapretMenuEnabled))
            {
                ScheduleUpdateMenu();
            }
        };
    }

    private void ScheduleUpdateMenu()
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher == null || dispatcher.CheckAccess())
        {
            UpdateMenu();
            return;
        }

        dispatcher.BeginInvoke(UpdateMenu);
    }

    public void UpdateMenu()
    {
        _enableItem.Enabled = !_viewModel.IsPacActive;
        _disableItem.Enabled = _viewModel.IsPacActive;

        if (_viewModel.IsTrayProxyMenuEnabled)
        {
            _proxyStartItem.Enabled = !_viewModel.IsProxyRunning && !_viewModel.IsBusy;
            _proxyStopItem.Enabled = _viewModel.IsProxyRunning && !_viewModel.IsBusy;
        }
        else
        {
            _proxyStartItem.Enabled = false;
            _proxyStopItem.Enabled = false;
        }

        if (_processModeStartItem != null && _processModeStopItem != null)
        {
            if (_viewModel.IsTrayProcessModeMenuEnabled)
            {
                _processModeStartItem.Enabled = !_viewModel.IsProcessModeRunning && !_viewModel.IsProcessModeOperating;
                _processModeStopItem.Enabled = _viewModel.IsProcessModeRunning && !_viewModel.IsProcessModeOperating;
            }
            else
            {
                _processModeStartItem.Enabled = false;
                _processModeStopItem.Enabled = false;
            }
        }

        if (_viewModel.IsTrayZapretMenuEnabled && !_viewModel.IsBypassProbingStrategy)
        {
            _zapretStartItem.Enabled = !_viewModel.IsBypassRunning && !_viewModel.IsBusy;
            _zapretStopItem.Enabled = _viewModel.IsBypassRunning && !_viewModel.IsBusy;
        }
        else
        {
            _zapretStartItem.Enabled = false;
            _zapretStopItem.Enabled = false;
        }
    }

    public void ExitApplication()
    {
        _allowClose = true;
        _notifyIcon.Visible = false;

        var app = Application.Current;
        if (app == null)
        {
            return;
        }

        app.Dispatcher.BeginInvoke(() =>
        {
            _viewModel.Shutdown();
            _window.Close();
            app.Shutdown();
        });
    }

    public void ShowMainWindow()
    {
        _window.Show();
        _window.WindowState = WindowState.Normal;
        _window.Activate();
        _notifyIcon.Visible = false;
    }

    public void MinimizeToTray(bool showNotification)
    {
        _window.Hide();
        _notifyIcon.Visible = true;

        if (showNotification && _viewModel.NotifyOnMinimizeToTray)
        {
            _notifyIcon.ShowBalloonTip(2000, AppBranding.DisplayName, "Приложение свёрнуто в трей.", ToolTipIcon.Info);
        }
    }

    private void Window_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (_allowClose)
        {
            return;
        }

        e.Cancel = true;
        _viewModel.SaveUiState();
        MinimizeToTray(showNotification: true);
    }

    public void Dispose()
    {
        _notifyIcon.Dispose();
    }

}
