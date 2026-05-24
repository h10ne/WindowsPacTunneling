using System.Diagnostics;
using WindowPacTunneling.Controls;
using WindowPacTunneling.Models;
using WindowPacTunneling.Services;
using WindowPacTunneling.Ui;

namespace WindowPacTunneling;

public partial class Form1 : Form
{
    private readonly DomainListService _domainListService = new();
    private readonly PacHttpServer _pacHttpServer = new();
    private readonly LocalProxyService _localProxyService = new();
    private readonly AppSettings _settings = SettingsService.Load();
    private readonly HashSet<string> _selectedListIds = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> _customDomains = [];
    private readonly List<string> _customIps = [];
    private NotifyIcon? _notifyIcon;
    private ToolStripMenuItem? _trayEnableItem;
    private ToolStripMenuItem? _trayDisableItem;
    private bool _allowClose;
    private System.Windows.Forms.Timer? _dailyUpdateTimer;

    public Form1()
    {
        InitializeComponent();
        InitializeAppearance();
        InitializeEvents();
        LoadSettingsToUi();
        InitializeTrayIcon();
        InitializeDailyUpdateTimer();
        UpdateTrayMenuState();
        Shown += Form1_Shown;
        FormClosing += Form1_FormClosing;
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        NativeTheme.ApplyDarkTitleBar(this);
    }

    private void InitializeAppearance()
    {
        UiStyler.ApplyForm(this);

        var proxyRow = TunnelingLayout.CreateProxyRow(lblProxy, cmbProxy, lblPacPort, cmbPacPort);
        TunnelingLayout.Configure(
            tabTunneling,
            proxyRow,
            lblAvailableLists,
            cmbAvailableLists,
            lblSelectedLists,
            pnlSelectedLists,
            lblCustomDomains,
            pnlCustomDomains,
            txtAddDomain,
            btnAddDomain,
            lblCustomIps,
            pnlCustomIps,
            txtAddIp,
            btnAddIp);

        ProxyLayout.Configure(
            tabProxy,
            lblProxyLink,
            txtProxyLink,
            lblLocalPort,
            txtLocalPort,
            lblProxyHint,
            lblProxyState,
            btnStartProxy,
            btnStopProxy);

        TunnelingLayout.ConfigureFooter(pnlFooter, btnApply, btnShowPac, btnDisable, lblStatus);
        TunnelingLayout.LayoutFooter(pnlFooter, btnApply, btnShowPac, btnDisable, lblStatus);

        pnlFooter.Paint += (_, e) =>
        {
            using var pen = new Pen(UiTheme.Border);
            e.Graphics.DrawLine(pen, 0, 0, pnlFooter.Width, 0);
        };

        tabMain.BackColor = UiTheme.Surface;

        DarkListPanel.ApplyPanelStyle(pnlSelectedLists);
        DarkListPanel.ApplyPanelStyle(pnlCustomDomains);
        DarkListPanel.ApplyPanelStyle(pnlCustomIps);

        UiStyler.StyleLabel(lblProxy);
        UiStyler.StyleLabel(lblPacPort);
        UiStyler.StyleLabel(lblAvailableLists);
        UiStyler.StyleLabel(lblSelectedLists);
        UiStyler.StyleLabel(lblCustomDomains);
        UiStyler.StyleLabel(lblCustomIps);
        UiStyler.StyleLabel(lblProxyLink);
        UiStyler.StyleLabel(lblLocalPort);
        UiStyler.StyleLabel(lblProxyHint, secondary: true);
        UiStyler.StyleLabel(lblProxyState, secondary: true);
        UiStyler.StyleComboBox(cmbProxy);
        UiStyler.StyleComboBox(cmbPacPort);
        UiStyler.StyleComboBox(cmbAvailableLists);
        UiStyler.StyleTextBox(txtAddDomain);
        UiStyler.StyleTextBox(txtAddIp);
        UiStyler.StyleTextBox(txtProxyLink);
        UiStyler.StyleTextBox(txtLocalPort);
        UiStyler.StyleCheckBox(chkStartWithWindows);
        UiStyler.StyleTabPage(tabTunneling);
        UiStyler.StyleTabPage(tabProxy);
        UiStyler.StyleTabPage(tabSettings);
        UiStyler.StylePrimaryButton(btnApply);
        UiStyler.StyleSecondaryButton(btnShowPac);
        UiStyler.StyleDangerButton(btnDisable);
        UiStyler.StylePrimaryButton(btnStartProxy);
        UiStyler.StyleDangerButton(btnStopProxy);
        UiStyler.StyleSecondaryButton(btnOpenDataFolder);
        UiStyler.StyleAccentIconButton(btnAddDomain);
        UiStyler.StyleAccentIconButton(btnAddIp);
        UiStyler.StyleStatusLabel(lblStatus);

        try
        {
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath)
                ?? new Icon(Path.Combine(AppContext.BaseDirectory, "Resources", "app.ico"));
        }
        catch
        {
        }
    }

    private void InitializeEvents()
    {
        btnApply.Click += async (_, _) => await ApplyAsync();
        btnShowPac.Click += async (_, _) => await ShowPacAsync();
        btnDisable.Click += (_, _) => DisableProxy();
        btnAddDomain.Click += (_, _) => AddCustomDomain();
        btnAddIp.Click += (_, _) => AddCustomIp();
        btnStartProxy.Click += async (_, _) => await StartLocalProxyAsync();
        btnStopProxy.Click += (_, _) => StopLocalProxy();
        txtAddDomain.KeyDown += (_, e) => { if (e.KeyCode == Keys.Enter) AddCustomDomain(); };
        txtAddIp.KeyDown += (_, e) => { if (e.KeyCode == Keys.Enter) AddCustomIp(); };
        cmbAvailableLists.SelectedIndexChanged += (_, _) => AddSelectedList();
        btnOpenDataFolder.Click += (_, _) => OpenDataFolder();
        _domainListService.StatusChanged += (_, message) => SetStatus(message);
    }

    private void InitializeDailyUpdateTimer()
    {
        _dailyUpdateTimer = new System.Windows.Forms.Timer { Interval = 60 * 60 * 1000 };
        _dailyUpdateTimer.Tick += async (_, _) =>
        {
            try
            {
                await _domainListService.EnsureUpdatedAsync();
            }
            catch
            {
            }
        };
        _dailyUpdateTimer.Start();
    }

    private void InitializeTrayIcon()
    {
        _notifyIcon = new NotifyIcon
        {
            Icon = Icon ?? SystemIcons.Shield,
            Text = "Window PAC Tunneling",
            Visible = false
        };

        var menu = new ContextMenuStrip();
        menu.Items.Add("Открыть", null, (_, _) => ShowFromTray());

        _trayEnableItem = new ToolStripMenuItem("Включить", null, async (_, _) => await EnableFromTrayAsync());
        _trayDisableItem = new ToolStripMenuItem("Отключить", null, (_, _) => DisableProxy());
        menu.Items.Add(_trayEnableItem);
        menu.Items.Add(_trayDisableItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Выход", null, (_, _) => ExitApplication());

        _notifyIcon.ContextMenuStrip = menu;
        _notifyIcon.DoubleClick += (_, _) => ShowFromTray();
    }

    private async void Form1_Shown(object? sender, EventArgs e)
    {
        NativeTheme.ApplyDarkTitleBar(this);
        SetUiEnabled(false);

        try
        {
            await _domainListService.UpdateAllListsAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                this,
                $"Не удалось обновить списки: {ex.Message}\nБудут использованы локальные копии, если они есть.",
                "Предупреждение",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }
        finally
        {
            SetUiEnabled(true);
            SetStatus("Готово");

            if (_settings.IsLocalProxyActive && !string.IsNullOrWhiteSpace(_settings.ProxyLink))
            {
                await StartLocalProxyAsync(silent: true);
            }
        }
    }

    private void LoadSettingsToUi()
    {
        cmbProxy.Items.Clear();
        foreach (var address in _settings.ProxyHistory.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            cmbProxy.Items.Add(address);
        }

        cmbProxy.Text = _settings.ProxyAddress;

        cmbPacPort.Items.Clear();
        foreach (var port in _settings.PacPortHistory.Distinct())
        {
            cmbPacPort.Items.Add(port.ToString());
        }

        cmbPacPort.Text = _settings.PacPort.ToString();
        _pacHttpServer.SetPort(_settings.PacPort);

        cmbAvailableLists.Items.Clear();
        cmbAvailableLists.Items.Add("-- выберите список --");
        foreach (var list in ServiceListDefinition.All)
        {
            cmbAvailableLists.Items.Add(list);
        }

        cmbAvailableLists.DisplayMember = "DisplayName";
        cmbAvailableLists.SelectedIndex = 0;

        foreach (var listId in _settings.SelectedListIds)
        {
            if (ServiceListDefinition.FindById(listId) != null)
            {
                _selectedListIds.Add(listId);
            }
        }

        RefreshSelectedListsPanel();
        _customDomains.AddRange(_settings.CustomDomains);
        _customIps.AddRange(_settings.CustomIps);
        RefreshCustomDomainsPanel();
        RefreshCustomIpsPanel();
        chkStartWithWindows.Checked = _settings.StartWithWindows;

        txtProxyLink.Text = _settings.ProxyLink;
        txtLocalPort.Text = _settings.LocalProxyPort.ToString();
        UpdateLocalProxyUi();

        if (_settings.IsProxyActive)
        {
            SetStatus("PAC был активен при прошлом запуске. Нажмите «Применить» для повторной активации.");
        }
    }

    private void RefreshSelectedListsPanel()
    {
        pnlSelectedLists.Controls.Clear();
        var y = 4;

        foreach (var listId in _selectedListIds.OrderBy(x => ServiceListDefinition.FindById(x)?.DisplayName))
        {
            var definition = ServiceListDefinition.FindById(listId);
            if (definition == null)
            {
                continue;
            }

            var capturedId = listId;
            DarkListPanel.AddRow(pnlSelectedLists, ref y, definition.DisplayName, () => RemoveSelectedList(capturedId));
        }
    }

    private void RefreshCustomDomainsPanel()
    {
        pnlCustomDomains.Controls.Clear();
        var y = 4;

        foreach (var domain in _customDomains.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
        {
            var captured = domain;
            DarkListPanel.AddRow(pnlCustomDomains, ref y, captured, () => RemoveCustomDomain(captured));
        }
    }

    private void RefreshCustomIpsPanel()
    {
        pnlCustomIps.Controls.Clear();
        var y = 4;

        foreach (var ip in _customIps.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
        {
            var captured = ip;
            DarkListPanel.AddRow(pnlCustomIps, ref y, captured, () => RemoveCustomIp(captured));
        }
    }

    private void AddSelectedList()
    {
        if (cmbAvailableLists.SelectedItem is not ServiceListDefinition definition)
        {
            return;
        }

        if (!_selectedListIds.Add(definition.Id))
        {
            return;
        }

        RefreshSelectedListsPanel();
        cmbAvailableLists.SelectedIndex = 0;
    }

    private void RemoveSelectedList(string listId)
    {
        _selectedListIds.Remove(listId);
        RefreshSelectedListsPanel();
    }

    private void AddCustomDomain()
    {
        var value = txtAddDomain.Text.Trim().TrimStart('.');
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        if (!ContainsIgnoreCase(_customDomains, value))
        {
            _customDomains.Add(value);
            RefreshCustomDomainsPanel();
        }

        txtAddDomain.Clear();
        txtAddDomain.Focus();
    }

    private void AddCustomIp()
    {
        var value = txtAddIp.Text.Trim();
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        if (!CidrEntry.TryParse(value, out _))
        {
            MessageBox.Show(this, "Некорректный IP или CIDR.", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (!ContainsIgnoreCase(_customIps, value))
        {
            _customIps.Add(value);
            RefreshCustomIpsPanel();
        }

        txtAddIp.Clear();
        txtAddIp.Focus();
    }

    private void RemoveCustomDomain(string domain)
    {
        _customDomains.RemoveAll(x => x.Equals(domain, StringComparison.OrdinalIgnoreCase));
        RefreshCustomDomainsPanel();
    }

    private void RemoveCustomIp(string ip)
    {
        _customIps.RemoveAll(x => x.Equals(ip, StringComparison.OrdinalIgnoreCase));
        RefreshCustomIpsPanel();
    }

    private static bool ContainsIgnoreCase(IEnumerable<string> items, string value) =>
        items.Any(x => x.Equals(value, StringComparison.OrdinalIgnoreCase));

    private async Task StartLocalProxyAsync(bool silent = false)
    {
        if (_localProxyService.IsRunning)
        {
            return;
        }

        if (!ProxyLinkParser.TryParse(txtProxyLink.Text, out var profile, out var parseError))
        {
            if (!silent)
            {
                MessageBox.Show(this, parseError, "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }

            return;
        }

        if (!InputParser.TryParsePort(txtLocalPort.Text, out var localPort, out var portError))
        {
            if (!silent)
            {
                MessageBox.Show(this, portError, "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }

            return;
        }

        SetProxyUiEnabled(false);

        try
        {
            var progress = new Progress<string>(message => SetProxyState(message));
            await _localProxyService.StartAsync(profile, localPort, progress, CancellationToken.None);

            SyncTunnelingProxyAddress(_localProxyService.LocalProxyAddress);
            SaveProxySettings(isActive: true);
            UpdateLocalProxyUi();
            SetProxyState($"Прокси запущен: {_localProxyService.LocalProxyAddress}");

            if (!silent)
            {
                SetStatus($"Локальный прокси: {_localProxyService.LocalProxyAddress}");
            }
        }
        catch (Exception ex)
        {
            UpdateLocalProxyUi();
            SetProxyState("Прокси остановлен");

            if (!silent)
            {
                MessageBox.Show(this, ex.Message, "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        finally
        {
            SetProxyUiEnabled(true);
        }
    }

    private void StopLocalProxy()
    {
        _localProxyService.Stop();
        SaveProxySettings(isActive: false);
        UpdateLocalProxyUi();
        SetProxyState("Прокси остановлен");
        SetStatus("Локальный прокси остановлен");
    }

    private void SyncTunnelingProxyAddress(string address)
    {
        if (!cmbProxy.Items.Contains(address))
        {
            cmbProxy.Items.Insert(0, address);
        }

        cmbProxy.Text = address;
    }

    private void UpdateLocalProxyUi()
    {
        var isRunning = _localProxyService.IsRunning;
        btnStartProxy.Enabled = !isRunning;
        btnStopProxy.Enabled = isRunning;
        txtProxyLink.Enabled = !isRunning;
        txtLocalPort.Enabled = !isRunning;

        if (isRunning)
        {
            SetProxyState($"Прокси запущен: {_localProxyService.LocalProxyAddress}");
        }
    }

    private void SaveProxySettings(bool isActive)
    {
        var link = txtProxyLink.Text.Trim();
        if (!string.IsNullOrWhiteSpace(link))
        {
            _settings.ProxyLink = link;
            _settings.ProxyLinkHistory.RemoveAll(x => x.Equals(link, StringComparison.OrdinalIgnoreCase));
            _settings.ProxyLinkHistory.Insert(0, link);
            _settings.ProxyLinkHistory = _settings.ProxyLinkHistory.Take(10).ToList();
        }

        if (InputParser.TryParsePort(txtLocalPort.Text, out var localPort, out _))
        {
            _settings.LocalProxyPort = localPort;
        }

        _settings.IsLocalProxyActive = isActive;
        SettingsService.Save(_settings);
    }

    private void SetProxyState(string message)
    {
        if (InvokeRequired)
        {
            BeginInvoke(() => lblProxyState.Text = message);
            return;
        }

        lblProxyState.Text = message;
    }

    private void SetProxyUiEnabled(bool enabled)
    {
        btnStartProxy.Enabled = enabled && !_localProxyService.IsRunning;
        btnStopProxy.Enabled = enabled && _localProxyService.IsRunning;
        txtProxyLink.Enabled = enabled && !_localProxyService.IsRunning;
        txtLocalPort.Enabled = enabled && !_localProxyService.IsRunning;
    }

    private async Task ShowPacAsync()
    {
        SetUiEnabled(false);

        try
        {
            var pac = await TryBuildPacContentAsync();
            if (pac == null)
            {
                if (!PacStorageService.TryRead(out var savedContent) || string.IsNullOrWhiteSpace(savedContent))
                {
                    return;
                }
            }
            else
            {
                await PacStorageService.SaveAsync(pac.Value.Content);
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = "notepad.exe",
                Arguments = AppPaths.PacFile,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            SetUiEnabled(true);
        }
    }

    private async Task ApplyAsync()
    {
        SetUiEnabled(false);

        try
        {
            if (!InputParser.TryParsePort(cmbPacPort.Text, out var pacPort, out var pacPortError))
            {
                MessageBox.Show(this, pacPortError, "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var pac = await TryBuildPacContentAsync();
            if (pac == null)
            {
                return;
            }

            await PacStorageService.SaveAsync(pac.Value.Content);

            _pacHttpServer.SetPort(pacPort);
            _pacHttpServer.SetPacContent(pac.Value.Content);
            _pacHttpServer.Restart();

            var pacUrl = _pacHttpServer.GetPacUrl(pac.Value.Hash);
            WindowsProxySettings.EnablePac(pacUrl);
            StartupService.SetEnabled(chkStartWithWindows.Checked);
            SaveSettings(pac.Value.Hash, isActive: true);

            SetStatus($"PAC активен: {pac.Value.DomainsCount} доменов, {pac.Value.SubnetsCount} подсетей");
            UpdateTrayMenuState();
            MessageBox.Show(
                this,
                $"PAC-файл применён.\n\nАдрес: {pacUrl}\nДоменов: {pac.Value.DomainsCount}\nПодсетей: {pac.Value.SubnetsCount}",
                "Готово",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
            SetStatus("Ошибка применения PAC");
        }
        finally
        {
            SetUiEnabled(true);
        }
    }

    private async Task EnableFromTrayAsync()
    {
        ShowFromTray();
        await ApplyAsync();
    }

    private async Task<(string Content, string Hash, int DomainsCount, int SubnetsCount)?> TryBuildPacContentAsync()
    {
        if (!InputParser.TryParseProxyAddress(cmbProxy.Text, out var host, out var port, out var error))
        {
            MessageBox.Show(this, error, "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return null;
        }

        if (_selectedListIds.Count == 0 && _customDomains.Count == 0 && _customIps.Count == 0)
        {
            MessageBox.Show(
                this,
                "Выберите хотя бы один список или укажите свои домены/IP.",
                "Ошибка",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return null;
        }

        await _domainListService.EnsureUpdatedAsync();

        var customDomains = _customDomains.ToList();
        var customIps = _customIps.ToList();
        var (domains, subnets) = await _domainListService.CollectEntriesAsync(
            _selectedListIds,
            customDomains,
            customIps);

        if (domains.Count == 0 && subnets.Count == 0)
        {
            MessageBox.Show(
                this,
                "Не найдено доменов или IP для формирования PAC.",
                "Ошибка",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return null;
        }

        var (content, hash) = PacGenerator.Generate(host, port, domains, subnets);
        return (content, hash, domains.Count, subnets.Count);
    }

    private void DisableProxy()
    {
        try
        {
            WindowsProxySettings.DisablePac();
            _pacHttpServer.Stop();
            SaveSettings(null, isActive: false);
            SetStatus("PAC отключён");
            UpdateTrayMenuState();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void SaveSettings(string? hash, bool isActive)
    {
        var address = cmbProxy.Text.Trim();
        if (!string.IsNullOrWhiteSpace(address))
        {
            _settings.ProxyAddress = address;
            _settings.ProxyHistory.RemoveAll(x => x.Equals(address, StringComparison.OrdinalIgnoreCase));
            _settings.ProxyHistory.Insert(0, address);
            _settings.ProxyHistory = _settings.ProxyHistory.Take(10).ToList();
        }

        if (InputParser.TryParsePort(cmbPacPort.Text, out var pacPort, out _))
        {
            _settings.PacPort = pacPort;
            _settings.PacPortHistory.Remove(pacPort);
            _settings.PacPortHistory.Insert(0, pacPort);
            _settings.PacPortHistory = _settings.PacPortHistory.Take(10).ToList();
        }

        _settings.SelectedListIds = _selectedListIds.ToList();
        _settings.CustomDomains = _customDomains.ToList();
        _settings.CustomIps = _customIps.ToList();
        _settings.StartWithWindows = chkStartWithWindows.Checked;
        _settings.IsProxyActive = isActive;
        _settings.ActivePacHash = hash;

        if (InputParser.TryParsePort(txtLocalPort.Text, out var localPort, out _))
        {
            _settings.LocalProxyPort = localPort;
        }

        var proxyLink = txtProxyLink.Text.Trim();
        if (!string.IsNullOrWhiteSpace(proxyLink))
        {
            _settings.ProxyLink = proxyLink;
        }

        _settings.IsLocalProxyActive = _localProxyService.IsRunning;
        SettingsService.Save(_settings);
    }

    private void SaveUiSettings()
    {
        SaveSettings(_settings.ActivePacHash, _settings.IsProxyActive);
        SaveProxySettings(_localProxyService.IsRunning);
    }

    private void OpenDataFolder()
    {
        AppPaths.EnsureRoot();
        Process.Start(new ProcessStartInfo
        {
            FileName = AppPaths.Root,
            UseShellExecute = true
        });
    }

    private void UpdateTrayMenuState()
    {
        if (_trayEnableItem == null || _trayDisableItem == null)
        {
            return;
        }

        var isActive = _settings.IsProxyActive && _pacHttpServer.IsRunning;
        _trayEnableItem.Enabled = !isActive;
        _trayDisableItem.Enabled = isActive;
    }

    private void SetUiEnabled(bool enabled)
    {
        tabMain.Enabled = enabled;
        btnApply.Enabled = enabled;
        btnShowPac.Enabled = enabled;
        btnDisable.Enabled = enabled;
        UpdateLocalProxyUi();
    }

    private void SetStatus(string message)
    {
        if (InvokeRequired)
        {
            BeginInvoke(() => lblStatus.Text = message);
            return;
        }

        lblStatus.Text = message;
    }

    private void Form1_FormClosing(object? sender, FormClosingEventArgs e)
    {
        if (_allowClose)
        {
            return;
        }

        e.Cancel = true;
        SaveUiSettings();
        Hide();
        _notifyIcon!.Visible = true;
        _notifyIcon.ShowBalloonTip(
            2000,
            "Window PAC Tunneling",
            "Приложение свёрнуто в трей.",
            ToolTipIcon.Info);
    }

    private void ShowFromTray()
    {
        Show();
        WindowState = FormWindowState.Normal;
        Activate();
        _notifyIcon!.Visible = false;
    }

    private void ExitApplication()
    {
        try
        {
            WindowsProxySettings.DisablePac();
            _pacHttpServer.Stop();
            _localProxyService.Stop();
            SaveSettings(null, isActive: false);
            SaveProxySettings(isActive: false);
        }
        catch
        {
        }

        _allowClose = true;
        _notifyIcon!.Visible = false;
        Application.Exit();
    }
}
