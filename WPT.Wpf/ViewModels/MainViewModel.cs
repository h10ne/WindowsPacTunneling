using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows.Threading;
using WPT.Core.Models;
using WPT.Core.Services;
using MessageBox = System.Windows.MessageBox;
using MessageBoxButton = System.Windows.MessageBoxButton;
using MessageBoxImage = System.Windows.MessageBoxImage;

namespace WPT.Wpf.ViewModels;

public sealed class MainViewModel : ViewModelBase
{
    private readonly DomainListService _domainListService = new();
    private readonly PacHttpServer _pacHttpServer = new();
    private readonly LocalProxyService _localProxyService = new();
    private readonly ProcessModeService _processModeService = new();
    private readonly AppSettings _settings = SettingsService.Load();
    private readonly HashSet<string> _selectedListIds = new(StringComparer.OrdinalIgnoreCase);
    private readonly DispatcherTimer _dailyUpdateTimer;
    private readonly DispatcherTimer _proxyHealthTimer;
    private readonly DispatcherTimer _processModeHealthTimer;
    private CancellationTokenSource? _proxyHealthCts;
    private CancellationTokenSource? _processModeHealthCts;
    private bool _isProxyHealthy;
    private bool _isProxyUnreachable;
    private bool _isProxyHealthChecking;
    private int? _proxyPingMs;
    private bool _isProcessModeHealthy;
    private bool _isProcessModeUnreachable;
    private bool _isProcessModeHealthChecking;
    private int? _processModePingMs;

    private string _proxyAddress = string.Empty;
    private string _pacPort = string.Empty;
    private string _proxyLink = string.Empty;
    private string _localPort = string.Empty;
    private string _processModeLink = string.Empty;
    private string _processModePort = string.Empty;
    private string _newProcessModeApp = string.Empty;
    private string _processModeStatus = "Process Mode: остановлен";
    private string _newDomain = string.Empty;
    private string _newIp = string.Empty;
    private string _statusMessage = "Готово";
    private string _footerRight = "PAC: выкл";
    private string _footerProxyStatus = "Прокси: остановлен";
    private string _footerProcessModeStatus = "PM: остановлен";
    private string _proxyState = "Прокси остановлен";
    private int _selectedSection;
    private bool _isBusy;
    private bool _startWithWindows;
    private bool _startProxyWithApp;
    private bool _startProcessModeWithApp;
    private bool _startMinimizedToTray;
    private bool _notifyOnMinimizeToTray;
    private bool _updateListsOnStartup;
    private bool _routeAllTrafficThroughProxy;
    private bool _showRussiaInsideRestrictionHint;
    private ServiceListDefinition? _selectedListToAdd;

    public MainViewModel()
    {
        SelectedLists = [];
        CustomDomains = [];
        CustomIps = [];
        ProcessModeApplications = [];
        ProxyHistory = [];
        PacPortHistory = [];
        AvailableLists = [.. ServiceListDefinition.All];

        ApplyCommand = new RelayCommand(async () => await ApplyAsync(), () => !IsBusy);
        ShowPacCommand = new RelayCommand(async () => await ShowPacAsync(), () => !IsBusy);
        DisablePacCommand = new RelayCommand(DisablePac, () => !IsBusy && IsPacActive);
        ToggleProxyCommand = new RelayCommand(async () => await ToggleProxyAsync(), () => !IsBusy);
        AddListCommand = new RelayCommand(AddSelectedList);
        RemoveListCommand = new RelayCommand(p => RemoveSelectedList((ListChipItem)p!));
        AddDomainCommand = new RelayCommand(AddCustomDomain);
        RemoveDomainCommand = new RelayCommand(p => RemoveCustomDomain((string)p!));
        AddIpCommand = new RelayCommand(AddCustomIp);
        RemoveIpCommand = new RelayCommand(p => RemoveCustomIp((string)p!));
        ApplyProcessModeCommand = new RelayCommand(ApplyProcessMode, () => !IsBusy && !IsProcessModeRunning);
        ToggleProcessModeCommand = new RelayCommand(async () => await ToggleProcessModeAsync(), () => !IsBusy);
        AddProcessModeAppCommand = new RelayCommand(AddProcessModeApp);
        RemoveProcessModeAppCommand = new RelayCommand(p => RemoveProcessModeApp((string)p!));
        PickRunningProcessCommand = new RelayCommand(_ => { });
        UpdateListsCommand = new RelayCommand(async () => await UpdateListsAsync());
        SaveSettingsCommand = new RelayCommand(SaveAppSettings);
        OpenDataFolderCommand = new RelayCommand(OpenDataFolder);

        _domainListService.StatusChanged += (_, message) => StatusMessage = message;

        _dailyUpdateTimer = new DispatcherTimer { Interval = TimeSpan.FromHours(1) };
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

        _proxyHealthTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
        _proxyHealthTimer.Tick += async (_, _) => await RefreshProxyHealthAsync();

        _processModeHealthTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        _processModeHealthTimer.Tick += async (_, _) => await RefreshProcessModeHealthAsync();

        LoadFromSettings();
    }

    public ObservableCollection<ListChipItem> SelectedLists { get; }

    public ObservableCollection<string> CustomDomains { get; }

    public ObservableCollection<string> CustomIps { get; }

    public ObservableCollection<string> ProcessModeApplications { get; }

    public ObservableCollection<string> ProxyHistory { get; }

    public ObservableCollection<string> PacPortHistory { get; }

    public IReadOnlyList<ServiceListDefinition> AvailableLists { get; }

    public string ProxyAddress
    {
        get => _proxyAddress;
        set => SetProperty(ref _proxyAddress, value);
    }

    public string PacPort
    {
        get => _pacPort;
        set => SetProperty(ref _pacPort, value);
    }

    public string ProxyLink
    {
        get => _proxyLink;
        set => SetProperty(ref _proxyLink, value);
    }

    public string LocalPort
    {
        get => _localPort;
        set => SetProperty(ref _localPort, value);
    }

    public string ProcessModeLink
    {
        get => _processModeLink;
        set => SetProperty(ref _processModeLink, value);
    }

    public string ProcessModePort
    {
        get => _processModePort;
        set => SetProperty(ref _processModePort, value);
    }

    public string NewProcessModeApp
    {
        get => _newProcessModeApp;
        set => SetProperty(ref _newProcessModeApp, value);
    }

    public string ProcessModeStatus
    {
        get => _processModeStatus;
        set => SetProperty(ref _processModeStatus, value);
    }

    public string NewDomain
    {
        get => _newDomain;
        set => SetProperty(ref _newDomain, value);
    }

    public string NewIp
    {
        get => _newIp;
        set => SetProperty(ref _newIp, value);
    }

    public ServiceListDefinition? SelectedListToAdd
    {
        get => _selectedListToAdd;
        set
        {
            SetProperty(ref _selectedListToAdd, value);
            if (value != null)
            {
                AddSelectedList();
            }
        }
    }

    public int SelectedSection
    {
        get => _selectedSection;
        set
        {
            if (!SetProperty(ref _selectedSection, value))
            {
                return;
            }

            OnPropertyChanged(nameof(IsTunnelingPage));
            OnPropertyChanged(nameof(IsProxyPage));
            OnPropertyChanged(nameof(IsProcessModePage));
            OnPropertyChanged(nameof(IsSettingsPage));
        }
    }

    public bool IsTunnelingPage
    {
        get => SelectedSection == 0;
        set { if (value) SelectedSection = 0; }
    }

    public bool IsProxyPage
    {
        get => SelectedSection == 1;
        set { if (value) SelectedSection = 1; }
    }

    public bool IsProcessModePage
    {
        get => SelectedSection == 2;
        set { if (value) SelectedSection = 2; }
    }

    public bool IsSettingsPage
    {
        get => SelectedSection == 3;
        set { if (value) SelectedSection = 3; }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public string FooterRight
    {
        get => _footerRight;
        set => SetProperty(ref _footerRight, value);
    }

    public string FooterProxyStatus
    {
        get => _footerProxyStatus;
        set => SetProperty(ref _footerProxyStatus, value);
    }

    public string FooterProcessModeStatus
    {
        get => _footerProcessModeStatus;
        set => SetProperty(ref _footerProcessModeStatus, value);
    }

    public string ProxyState
    {
        get => _proxyState;
        set => SetProperty(ref _proxyState, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        set
        {
            if (SetProperty(ref _isBusy, value))
            {
                OnPropertyChanged(nameof(IsProxyEditingEnabled));
                OnPropertyChanged(nameof(IsProcessModeEditingEnabled));
                RelayCommand.RaiseAllCanExecuteChanged();
            }
        }
    }

    private bool _isPacActive;

    public bool IsPacActive
    {
        get => _isPacActive;
        set
        {
            if (SetProperty(ref _isPacActive, value))
            {
                RelayCommand.RaiseAllCanExecuteChanged();
            }
        }
    }

    public bool IsProxyRunning => _localProxyService.IsRunning;

    public bool IsProxyHealthy => _isProxyHealthy;

    public bool IsProxyUnreachable => _isProxyUnreachable;

    public bool IsProcessModeHealthy => _isProcessModeHealthy;

    public bool IsProcessModeUnreachable => _isProcessModeUnreachable;

    public bool IsProcessModeRunning => _processModeService.IsRunning;

    public bool IsProxyEditingEnabled => !IsProxyRunning && !IsBusy;

    public bool IsProcessModeEditingEnabled => !IsProcessModeRunning && !IsBusy;

    public string ProxyToggleLabel => IsProxyRunning ? "Остановить" : "Запустить";

    public string ProcessModeToggleLabel => IsProcessModeRunning ? "Остановить" : "Запустить";

    public bool StartWithWindows
    {
        get => _startWithWindows;
        set => SetProperty(ref _startWithWindows, value);
    }

    public bool StartProxyWithApp
    {
        get => _startProxyWithApp;
        set => SetProperty(ref _startProxyWithApp, value);
    }

    public bool StartProcessModeWithApp
    {
        get => _startProcessModeWithApp;
        set => SetProperty(ref _startProcessModeWithApp, value);
    }

    public bool StartMinimizedToTray
    {
        get => _startMinimizedToTray;
        set => SetProperty(ref _startMinimizedToTray, value);
    }

    public bool NotifyOnMinimizeToTray
    {
        get => _notifyOnMinimizeToTray;
        set => SetProperty(ref _notifyOnMinimizeToTray, value);
    }

    public bool UpdateListsOnStartup
    {
        get => _updateListsOnStartup;
        set => SetProperty(ref _updateListsOnStartup, value);
    }

    public bool RouteAllTrafficThroughProxy
    {
        get => _routeAllTrafficThroughProxy;
        set
        {
            if (SetProperty(ref _routeAllTrafficThroughProxy, value))
            {
                OnPropertyChanged(nameof(IsTunnelingListsEnabled));
            }
        }
    }

    public bool IsTunnelingListsEnabled => !RouteAllTrafficThroughProxy;

    public bool ShowRussiaInsideRestrictionHint
    {
        get => _showRussiaInsideRestrictionHint;
        private set => SetProperty(ref _showRussiaInsideRestrictionHint, value);
    }

    public string RussiaInsideRestrictionHint => RussiaInsideListRules.InfoBarText;

    public RelayCommand ApplyCommand { get; }

    public RelayCommand ShowPacCommand { get; }

    public RelayCommand DisablePacCommand { get; }

    public RelayCommand ToggleProxyCommand { get; }

    public RelayCommand AddListCommand { get; }

    public RelayCommand RemoveListCommand { get; }

    public RelayCommand AddDomainCommand { get; }

    public RelayCommand RemoveDomainCommand { get; }

    public RelayCommand AddIpCommand { get; }

    public RelayCommand RemoveIpCommand { get; }

    public RelayCommand ApplyProcessModeCommand { get; }

    public RelayCommand ToggleProcessModeCommand { get; }

    public RelayCommand AddProcessModeAppCommand { get; }

    public RelayCommand RemoveProcessModeAppCommand { get; }

    public RelayCommand PickRunningProcessCommand { get; }

    public RelayCommand UpdateListsCommand { get; }

    public RelayCommand SaveSettingsCommand { get; }

    public RelayCommand OpenDataFolderCommand { get; }

    public async Task InitializeAsync()
    {
        IsBusy = true;

        try
        {
            if (UpdateListsOnStartup)
            {
                await UpdateListsAsync(showWarningOnError: true, reEnableUi: false);
            }

            if (_settings.IsLocalProxyActive && !string.IsNullOrWhiteSpace(_settings.ProxyLink))
            {
                if (_localProxyService.IsRunning)
                {
                    UpdateProxyUi();
                    _ = RefreshProxyHealthAsync();
                }
                else
                {
                    await StartLocalProxyAsync(silent: true);
                }
            }

            if (_settings.IsProcessModeActive
                && !string.IsNullOrWhiteSpace(_settings.ProcessModeLink)
                && ProcessModeApplications.Count > 0)
            {
                if (_processModeService.IsRunning)
                {
                    UpdateProcessModeUi();
                    _ = RefreshProcessModeHealthAsync();
                }
                else
                {
                    await RestoreProcessModeAsync(silent: true);
                }
            }
            else if (StartProcessModeWithApp
                && !string.IsNullOrWhiteSpace(_settings.ProcessModeLink)
                && ProcessModeApplications.Count > 0)
            {
                await StartProcessModeAsync(silent: true);
            }

            if (StartProxyWithApp && _settings.IsProxyActive)
            {
                await ApplyAsync(silent: true);
            }

            if (_settings.IsProxyActive && !StartProxyWithApp)
            {
                StatusMessage = "PAC был активен. Нажмите «Применить» для повторной активации.";
            }
        }
        finally
        {
            IsBusy = false;
            RefreshPacState();
        }
    }

    public void SaveUiState()
    {
        UpdateTunnelingPreferences();
        UpdateProcessModePreferences();
        SaveProxySettings(_localProxyService.IsRunning);
        SaveProcessModeSettings(_processModeService.IsRunning);
    }

    public async Task ApplyFromTrayAsync()
    {
        await ApplyAsync();
    }

    public void StopLocalProxyOnExit()
    {
        try
        {
            _localProxyService.Stop();
            SaveProxySettings(isActive: false);
            ResetProxyHealth();
            UpdateProxyUi();
        }
        catch
        {
        }
    }

    public void StopProcessModeOnExit()
    {
        try
        {
            _processModeService.Stop();
            SaveProcessModeSettings(isActive: false);
            ResetProcessModeHealth();
            UpdateProcessModeUi();
        }
        catch
        {
        }
    }

    public void Shutdown()
    {
        try
        {
            _pacHttpServer.Stop();
            StopLocalProxyOnExit();
            StopProcessModeOnExit();
            SaveUiState();
        }
        catch
        {
        }

        _domainListService.Dispose();
        _pacHttpServer.Dispose();
        _localProxyService.Dispose();
        _processModeService.Dispose();
    }

    private void LoadFromSettings()
    {
        foreach (var address in _settings.ProxyHistory.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            ProxyHistory.Add(address);
        }

        ProxyAddress = _settings.ProxyAddress;

        foreach (var port in _settings.PacPortHistory.Distinct())
        {
            PacPortHistory.Add(port.ToString());
        }

        PacPort = _settings.PacPort.ToString();
        _pacHttpServer.SetPort(_settings.PacPort);

        foreach (var listId in _settings.SelectedListIds)
        {
            var definition = ServiceListDefinition.FindById(listId);
            if (definition != null && _selectedListIds.Add(listId))
            {
                SelectedLists.Add(new ListChipItem(definition.Id, definition.DisplayName));
            }
        }

        if (_selectedListIds.Contains(RussiaInsideListRules.RussiaInsideId))
        {
            RemoveListsNotAllowedWithRussiaInside();
        }

        foreach (var domain in _settings.CustomDomains.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
        {
            CustomDomains.Add(domain);
        }

        foreach (var ip in _settings.CustomIps.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
        {
            CustomIps.Add(ip);
        }

        StartWithWindows = _settings.StartWithWindows;
        StartProxyWithApp = _settings.StartProxyWithApp;
        StartProcessModeWithApp = _settings.StartProcessModeWithApp;
        StartMinimizedToTray = _settings.StartMinimizedToTray;
        NotifyOnMinimizeToTray = _settings.NotifyOnMinimizeToTray;
        UpdateListsOnStartup = _settings.UpdateListsOnStartup;
        RouteAllTrafficThroughProxy = _settings.RouteAllTrafficThroughProxy;
        ProxyLink = _settings.ProxyLink;
        LocalPort = _settings.LocalProxyPort.ToString();
        if (InputParser.TryParsePort(LocalPort, out var localPort, out _))
        {
            _localProxyService.Prepare(localPort);
        }

        ProcessModeLink = _settings.ProcessModeLink;
        ProcessModePort = _settings.ProcessModePort.ToString();
        if (InputParser.TryParsePort(ProcessModePort, out var processModePort, out _))
        {
            _processModeService.Prepare(processModePort);
        }

        foreach (var app in _settings.ProcessModeApplications.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
        {
            ProcessModeApplications.Add(app);
        }

        UpdateProxyUi();
        if (_localProxyService.IsRunning)
        {
            _ = RefreshProxyHealthAsync();
        }

        UpdateProcessModeUi();
        if (_processModeService.IsRunning)
        {
            _ = RefreshProcessModeHealthAsync();
        }

        RefreshPacState();
    }

    public void RefreshPacState()
    {
        var systemEnabled = WindowsProxySettings.IsPacEnabled(out _);
        IsPacActive = systemEnabled;

        if (systemEnabled != _settings.IsProxyActive)
        {
            _settings.IsProxyActive = systemEnabled;
        }

        UpdateFooter();
        RelayCommand.RaiseAllCanExecuteChanged();
    }

    private void AddSelectedList()
    {
        if (_selectedListToAdd == null)
        {
            return;
        }

        var list = _selectedListToAdd;
        ResetSelectedListToAdd();

        if (_selectedListIds.Contains(list.Id))
        {
            return;
        }

        var hasRussiaInside = _selectedListIds.Contains(RussiaInsideListRules.RussiaInsideId);

        if (hasRussiaInside)
        {
            if (RussiaInsideListRules.IsIncludedInRussiaInside(list.Id))
            {
                ShowRussiaInsideRestrictionHint = true;
                return;
            }

            if (!RussiaInsideListRules.IsAllowedWithRussiaInside(list.Id))
            {
                return;
            }
        }

        if (RussiaInsideListRules.IsRussiaInside(list.Id))
        {
            var hadIncludedLists = _selectedListIds.Any(RussiaInsideListRules.IsIncludedInRussiaInside);
            RemoveListsNotAllowedWithRussiaInside();

            if (hadIncludedLists)
            {
                ShowRussiaInsideRestrictionHint = true;
            }
        }

        _selectedListIds.Add(list.Id);
        SelectedLists.Add(new ListChipItem(list.Id, list.DisplayName));
    }

    private void ResetSelectedListToAdd()
    {
        if (_selectedListToAdd == null)
        {
            return;
        }

        _selectedListToAdd = null;
        OnPropertyChanged(nameof(SelectedListToAdd));
    }

    private void RemoveSelectedList(ListChipItem item)
    {
        _selectedListIds.Remove(item.Id);
        SelectedLists.Remove(item);
    }

    private void RemoveListsNotAllowedWithRussiaInside()
    {
        foreach (var item in SelectedLists.Where(x => !RussiaInsideListRules.IsAllowedWithRussiaInside(x.Id)).ToList())
        {
            _selectedListIds.Remove(item.Id);
            SelectedLists.Remove(item);
        }
    }

    private void AddCustomDomain()
    {
        var value = NewDomain.Trim().TrimStart('.');
        if (string.IsNullOrWhiteSpace(value) || ContainsIgnoreCase(CustomDomains, value))
        {
            return;
        }

        CustomDomains.Add(value);
        NewDomain = string.Empty;
    }

    private void AddCustomIp()
    {
        var value = NewIp.Trim();
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        if (!CidrEntry.TryParse(value, out _))
        {
            MessageBox.Show("Некорректный IP или CIDR.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (ContainsIgnoreCase(CustomIps, value))
        {
            return;
        }

        CustomIps.Add(value);
        NewIp = string.Empty;
    }

    private void RemoveCustomDomain(string domain) => CustomDomains.Remove(domain);

    private void RemoveCustomIp(string ip) => CustomIps.Remove(ip);

    private void AddProcessModeApp()
    {
        var value = NewProcessModeApp.Trim();
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        if (!value.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            value += ".exe";
        }

        if (ContainsIgnoreCase(ProcessModeApplications, value))
        {
            return;
        }

        ProcessModeApplications.Add(value);
        NewProcessModeApp = string.Empty;
    }

    private void RemoveProcessModeApp(string app) => ProcessModeApplications.Remove(app);

    private void ApplyProcessMode()
    {
        if (IsProcessModeRunning)
        {
            return;
        }

        if (!InputParser.TryParsePort(ProcessModePort, out var port, out var portError))
        {
            MessageBox.Show(portError, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!string.IsNullOrWhiteSpace(ProcessModeLink)
            && !ProxyLinkParser.TryParse(ProcessModeLink, out _, out var parseError))
        {
            MessageBox.Show(parseError, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        _processModeService.Prepare(port);
        SaveProcessModeSettings(isActive: _processModeService.IsRunning);
        StatusMessage = "Настройки Process Mode сохранены";
        MessageBox.Show(
            "Настройки Process Mode сохранены.",
            "Готово",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private async Task ToggleProcessModeAsync()
    {
        if (_processModeService.IsRunning)
        {
            StopProcessMode();
            return;
        }

        if (InputParser.TryParsePort(ProcessModePort, out var port, out _))
        {
            _processModeService.Prepare(port);
        }

        await StartProcessModeAsync();
    }

    private async Task RestoreProcessModeAsync(bool silent = false)
    {
        if (_processModeService.IsRunning)
        {
            return;
        }

        if (ProcessModeApplications.Count == 0)
        {
            return;
        }

        if (!ProxyLinkParser.TryParse(ProcessModeLink, out var profile, out var parseError))
        {
            if (!silent)
            {
                MessageBox.Show(parseError, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
            }

            return;
        }

        if (!InputParser.TryParsePort(ProcessModePort, out var localPort, out var portError))
        {
            if (!silent)
            {
                MessageBox.Show(portError, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
            }

            return;
        }

        IsBusy = true;

        try
        {
            var progress = new Progress<string>(message => ProcessModeStatus = message);
            await _processModeService.TryRestoreAsync(
                profile,
                localPort,
                ProcessModeApplications.ToList(),
                progress,
                CancellationToken.None);

            SaveProcessModeSettings(isActive: true);
            UpdateProcessModeUi();
            _ = RefreshProcessModeHealthAsync(showCheckingState: false);

            if (!silent)
            {
                StatusMessage = $"Process Mode восстановлен: {_processModeService.LocalProxyAddress}";
            }
        }
        catch (Exception ex)
        {
            UpdateProcessModeUi();
            if (!silent)
            {
                MessageBox.Show(ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task StartProcessModeAsync(bool silent = false)
    {
        if (_processModeService.IsRunning)
        {
            return;
        }

        if (ProcessModeApplications.Count == 0)
        {
            if (!silent)
            {
                MessageBox.Show(
                    "Добавьте хотя бы одно приложение в список.",
                    "Ошибка",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }

            return;
        }

        if (!ProxyLinkParser.TryParse(ProcessModeLink, out var profile, out var parseError))
        {
            if (!silent)
            {
                MessageBox.Show(parseError, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
            }

            return;
        }

        if (!InputParser.TryParsePort(ProcessModePort, out var localPort, out var portError))
        {
            if (!silent)
            {
                MessageBox.Show(portError, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
            }

            return;
        }

        IsBusy = true;

        try
        {
            var progress = new Progress<string>(message => ProcessModeStatus = message);
            await _processModeService.StartAsync(
                profile,
                localPort,
                ProcessModeApplications.ToList(),
                progress,
                CancellationToken.None);

            SaveProcessModeSettings(isActive: true);
            UpdateProcessModeUi();
            _ = RefreshProcessModeHealthAsync(showCheckingState: true);

            if (!silent)
            {
                StatusMessage = $"Process Mode: {_processModeService.LocalProxyAddress}";
            }
        }
        catch (Exception ex)
        {
            UpdateProcessModeUi();
            if (!silent)
            {
                MessageBox.Show(ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void StopProcessMode()
    {
        _processModeService.Stop();
        SaveProcessModeSettings(isActive: false);
        ResetProcessModeHealth();
        UpdateProcessModeUi();
        StatusMessage = "Process Mode остановлен";
    }

    private void UpdateProcessModeUi()
    {
        var isRunning = _processModeService.IsRunning;
        var address = _processModeService.LocalProxyAddress;

        if (!isRunning)
        {
            FooterProcessModeStatus = "PM: остановлен";
            ProcessModeStatus = "Process Mode: остановлен";
        }
        else if (_isProcessModeHealthChecking && _processModePingMs == null)
        {
            FooterProcessModeStatus = "PM: проверка";
            ProcessModeStatus =
                $"Process Mode: работает · {address} · Redirector · {ProcessModeApplications.Count} прилож.";
        }
        else if (_isProcessModeHealthy)
        {
            FooterProcessModeStatus = $"PM: работает · {_processModePingMs} мс";
            ProcessModeStatus =
                $"Process Mode: работает · {address} · Redirector · {ProcessModeApplications.Count} прилож. · {_processModePingMs} мс";
        }
        else if (_isProcessModeUnreachable)
        {
            FooterProcessModeStatus = "PM: нет доступа";
            ProcessModeStatus =
                $"Process Mode: нет доступа · {address} · Redirector · {ProcessModeApplications.Count} прилож.";
        }
        else
        {
            var apps = ProcessModeApplications.Count;
            FooterProcessModeStatus = "PM: работает";
            ProcessModeStatus =
                $"Process Mode: работает · {address} · Redirector · {apps} прилож.";
        }

        OnPropertyChanged(nameof(IsProcessModeRunning));
        OnPropertyChanged(nameof(IsProcessModeEditingEnabled));
        OnPropertyChanged(nameof(ProcessModeToggleLabel));
        OnPropertyChanged(nameof(IsProcessModeHealthy));
        OnPropertyChanged(nameof(IsProcessModeUnreachable));
        RelayCommand.RaiseAllCanExecuteChanged();
    }

    private static bool ContainsIgnoreCase(IEnumerable<string> items, string value) =>
        items.Any(x => x.Equals(value, StringComparison.OrdinalIgnoreCase));

    private async Task ToggleProxyAsync()
    {
        if (_localProxyService.IsRunning)
        {
            StopLocalProxy();
            return;
        }

        if (InputParser.TryParsePort(LocalPort, out var localPort, out _))
        {
            _localProxyService.Prepare(localPort);
        }

        await StartLocalProxyAsync();
    }

    private async Task StartLocalProxyAsync(bool silent = false)
    {
        if (_localProxyService.IsRunning)
        {
            return;
        }

        if (!ProxyLinkParser.TryParse(ProxyLink, out var profile, out var parseError))
        {
            if (!silent)
            {
                MessageBox.Show(parseError, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
            }

            return;
        }

        if (!InputParser.TryParsePort(LocalPort, out var localPort, out var portError))
        {
            if (!silent)
            {
                MessageBox.Show(portError, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
            }

            return;
        }

        IsBusy = true;

        try
        {
            var progress = new Progress<string>(message => ProxyState = message);
            await _localProxyService.StartAsync(profile, localPort, progress, CancellationToken.None);

            SaveProxySettings(isActive: true);
            UpdateProxyUi();
            UpdateFooter();
            _ = RefreshProxyHealthAsync();

            if (!silent)
            {
                StatusMessage = $"Локальный прокси: {_localProxyService.LocalProxyAddress}";
            }
        }
        catch (Exception ex)
        {
            ResetProxyHealth();
            UpdateProxyUi();
            if (!silent)
            {
                MessageBox.Show(ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void StopLocalProxy()
    {
        _localProxyService.Stop();
        SaveProxySettings(isActive: false);
        ResetProxyHealth();
        UpdateProxyUi();
        UpdateFooter();
    }

    private void UpdateProxyUi()
    {
        var isRunning = _localProxyService.IsRunning;
        var address = _localProxyService.LocalProxyAddress;

        if (!isRunning)
        {
            FooterProxyStatus = "Прокси: остановлен";
            ProxyState = "Остановлен";
        }
        else if (_isProxyHealthChecking)
        {
            FooterProxyStatus = $"Прокси: проверка ·";
            ProxyState = $"Проверка · {address}";
        }
        else if (_isProxyHealthy)
        {
            FooterProxyStatus = $"Прокси: работает · {_proxyPingMs} мс";
            ProxyState = $"Работает · {address} · {_proxyPingMs} мс";
        }
        else if (_isProxyUnreachable)
        {
            FooterProxyStatus = $"Прокси: нет доступа";
            ProxyState = $"Нет доступа · {address}";
        }
        else
        {
            FooterProxyStatus = $"Прокси: работает · {address}";
            ProxyState = $"Работает · {address}";
        }

        OnPropertyChanged(nameof(IsProxyRunning));
        OnPropertyChanged(nameof(IsProxyHealthy));
        OnPropertyChanged(nameof(IsProxyUnreachable));
        OnPropertyChanged(nameof(IsProxyEditingEnabled));
        OnPropertyChanged(nameof(ProxyToggleLabel));
    }

    private async Task RefreshProxyHealthAsync()
    {
        if (!_localProxyService.IsRunning || _localProxyService.LocalPort <= 0)
        {
            return;
        }

        _proxyHealthCts?.Cancel();
        _proxyHealthCts?.Dispose();
        _proxyHealthCts = new CancellationTokenSource();
        var cancellationToken = _proxyHealthCts.Token;

        SetProxyHealthState(isChecking: true, isHealthy: false, isUnreachable: false, pingMs: null);

        try
        {
            var result = await ProxyHealthChecker.CheckAsync(_localProxyService.LocalPort, cancellationToken);
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            SetProxyHealthState(
                isChecking: false,
                isHealthy: result.IsReachable,
                isUnreachable: !result.IsReachable,
                pingMs: result.LatencyMs);
        }
        catch (OperationCanceledException)
        {
        }
        catch
        {
            if (!cancellationToken.IsCancellationRequested)
            {
                SetProxyHealthState(
                    isChecking: false,
                    isHealthy: false,
                    isUnreachable: true,
                    pingMs: null);
            }
        }
    }

    private void SetProxyHealthState(bool isChecking, bool isHealthy, bool isUnreachable, int? pingMs)
    {
        _isProxyHealthChecking = isChecking;
        _isProxyHealthy = isHealthy;
        _isProxyUnreachable = isUnreachable;
        _proxyPingMs = pingMs;

        if (_localProxyService.IsRunning && !isChecking)
        {
            _proxyHealthTimer.Start();
        }
        else if (!isChecking)
        {
            _proxyHealthTimer.Stop();
        }

        UpdateProxyUi();
    }

    private void ResetProxyHealth()
    {
        _proxyHealthCts?.Cancel();
        _proxyHealthCts?.Dispose();
        _proxyHealthCts = null;
        _proxyHealthTimer.Stop();
        _isProxyHealthChecking = false;
        _isProxyHealthy = false;
        _isProxyUnreachable = false;
        _proxyPingMs = null;
        OnPropertyChanged(nameof(IsProxyHealthy));
        OnPropertyChanged(nameof(IsProxyUnreachable));
    }

    private async Task RefreshProcessModeHealthAsync(bool showCheckingState = false)
    {
        if (!_processModeService.IsRunning || _processModeService.LocalPort <= 0)
        {
            return;
        }

        _processModeHealthCts?.Cancel();
        _processModeHealthCts?.Dispose();
        _processModeHealthCts = new CancellationTokenSource();
        var cancellationToken = _processModeHealthCts.Token;

        if (showCheckingState)
        {
            _isProcessModeHealthChecking = true;
            UpdateProcessModeUi();
        }

        try
        {
            var result = await ProxyHealthChecker.CheckAsync(_processModeService.LocalPort, cancellationToken);
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            ApplyProcessModeHealthResult(result);
        }
        catch (OperationCanceledException)
        {
        }
        catch
        {
            if (!cancellationToken.IsCancellationRequested)
            {
                ApplyProcessModeHealthResult(new ProxyHealthResult(false, null));
            }
        }
        finally
        {
            if (showCheckingState && cancellationToken.IsCancellationRequested)
            {
                _isProcessModeHealthChecking = false;
                UpdateProcessModeUi();
            }
        }
    }

    private void ApplyProcessModeHealthResult(ProxyHealthResult result)
    {
        _isProcessModeHealthChecking = false;
        _isProcessModeHealthy = result.IsReachable;
        _isProcessModeUnreachable = !result.IsReachable;
        _processModePingMs = result.LatencyMs;

        if (_processModeService.IsRunning)
        {
            _processModeHealthTimer.Start();
        }
        else
        {
            _processModeHealthTimer.Stop();
        }

        UpdateProcessModeUi();
    }

    private void ResetProcessModeHealth()
    {
        _processModeHealthCts?.Cancel();
        _processModeHealthCts?.Dispose();
        _processModeHealthCts = null;
        _processModeHealthTimer.Stop();
        _isProcessModeHealthChecking = false;
        _isProcessModeHealthy = false;
        _isProcessModeUnreachable = false;
        _processModePingMs = null;
        OnPropertyChanged(nameof(IsProcessModeHealthy));
        OnPropertyChanged(nameof(IsProcessModeUnreachable));
    }

    private async Task UpdateListsAsync(bool showWarningOnError = true, bool reEnableUi = true)
    {
        if (reEnableUi)
        {
            IsBusy = true;
        }

        try
        {
            await _domainListService.UpdateAllListsAsync();
        }
        catch (Exception ex)
        {
            if (showWarningOnError)
            {
                MessageBox.Show(
                    $"Не удалось обновить списки: {ex.Message}\nБудут использованы локальные копии, если они есть.",
                    "Предупреждение",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }
        finally
        {
            if (reEnableUi)
            {
                IsBusy = false;
            }
        }
    }

    private async Task ShowPacAsync()
    {
        IsBusy = true;

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
            MessageBox.Show(ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task ApplyAsync(bool silent = false)
    {
        ShowRussiaInsideRestrictionHint = false;
        IsBusy = true;

        try
        {
            if (!InputParser.TryParsePort(PacPort, out var pacPort, out var pacPortError))
            {
                if (!silent)
                {
                    MessageBox.Show(pacPortError, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                }

                return;
            }

            var pac = await TryBuildPacContentAsync(silent);
            if (pac == null)
            {
                if (silent)
                {
                    StatusMessage = "Не удалось автоматически запустить PAC";
                }

                return;
            }

            await PacStorageService.SaveAsync(pac.Value.Content);

            _pacHttpServer.SetPort(pacPort);
            _pacHttpServer.SetPacContent(pac.Value.Content);
            _pacHttpServer.Restart();

            var pacUrl = _pacHttpServer.GetPacUrl(pac.Value.Hash);
            WindowsProxySettings.EnablePac(pacUrl);
            StartupService.SetEnabled(StartWithWindows);
            SaveSettings(pac.Value.Hash, isActive: true);

            RefreshPacState();

            if (RouteAllTrafficThroughProxy)
            {
                StatusMessage = "PAC активен: весь трафик через прокси";
            }
            else
            {
                StatusMessage = $"PAC активен: {pac.Value.DomainsCount} доменов, {pac.Value.SubnetsCount} подсетей";
            }

            if (!silent)
            {
                var details = RouteAllTrafficThroughProxy
                    ? "Режим: весь трафик через прокси"
                    : $"Доменов: {pac.Value.DomainsCount}\nПодсетей: {pac.Value.SubnetsCount}";

                MessageBox.Show(
                    $"PAC-файл применён.\n\nАдрес: {pacUrl}\n{details}",
                    "Готово",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            if (!silent)
            {
                MessageBox.Show(ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            StatusMessage = "Ошибка применения PAC";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task<(string Content, string Hash, int DomainsCount, int SubnetsCount)?> TryBuildPacContentAsync(bool silent = false)
    {
        if (!InputParser.TryParseProxyAddress(ProxyAddress, out var host, out var port, out var error))
        {
            if (!silent)
            {
                MessageBox.Show(error, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
            }

            return null;
        }

        if (RouteAllTrafficThroughProxy)
        {
            var allTraffic = PacGenerator.GenerateAllTraffic(host, port);
            return (allTraffic.Content, allTraffic.Hash, 0, 0);
        }

        if (_selectedListIds.Count == 0 && CustomDomains.Count == 0 && CustomIps.Count == 0)
        {
            if (!silent)
            {
                MessageBox.Show(
                    "Выберите хотя бы один список или укажите свои домены/IP.",
                    "Ошибка",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }

            return null;
        }

        await _domainListService.EnsureUpdatedAsync();

        var (domains, subnets) = await _domainListService.CollectEntriesAsync(
            _selectedListIds,
            CustomDomains.ToList(),
            CustomIps.ToList());

        if (domains.Count == 0 && subnets.Count == 0)
        {
            if (!silent)
            {
                MessageBox.Show(
                    "Не найдено доменов или IP для формирования PAC.",
                    "Ошибка",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }

            return null;
        }

        var (content, hash) = PacGenerator.Generate(host, port, domains, subnets);
        return (content, hash, domains.Count, subnets.Count);
    }

    private void DisablePac()
    {
        try
        {
            WindowsProxySettings.DisablePac();
            _pacHttpServer.Stop();
            SaveSettings(null, isActive: false);
            RefreshPacState();
            StatusMessage = "PAC отключён";
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void SaveAppSettings()
    {
        try
        {
            UpdateAppSettingsPreferences();
            StartupService.SetEnabled(StartWithWindows);
            SettingsService.Save(_settings);
            StatusMessage = "Настройки сохранены";
            MessageBox.Show(
                "Настройки сохранены.",
                "Готово",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
        }
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

    private void SaveSettings(string? hash, bool isActive)
    {
        UpdateTunnelingPreferences();
        UpdateAppSettingsPreferences();
        _settings.IsProxyActive = isActive;
        _settings.ActivePacHash = hash;

        if (InputParser.TryParsePort(LocalPort, out var localPort, out _))
        {
            _settings.LocalProxyPort = localPort;
        }

        if (!string.IsNullOrWhiteSpace(ProxyLink))
        {
            _settings.ProxyLink = ProxyLink.Trim();
        }

        _settings.IsLocalProxyActive = _localProxyService.IsRunning;
        SettingsService.Save(_settings);
    }

    private void SaveProxySettings(bool isActive)
    {
        var link = ProxyLink.Trim();
        if (!string.IsNullOrWhiteSpace(link))
        {
            _settings.ProxyLink = link;
            _settings.ProxyLinkHistory.RemoveAll(x => x.Equals(link, StringComparison.OrdinalIgnoreCase));
            _settings.ProxyLinkHistory.Insert(0, link);
            _settings.ProxyLinkHistory = _settings.ProxyLinkHistory.Take(10).ToList();
        }

        if (InputParser.TryParsePort(LocalPort, out var localPort, out _))
        {
            _settings.LocalProxyPort = localPort;
        }

        _settings.IsLocalProxyActive = isActive;
        SettingsService.Save(_settings);
    }

    private void SaveProcessModeSettings(bool isActive)
    {
        UpdateProcessModePreferences();

        if (!string.IsNullOrWhiteSpace(ProcessModeLink))
        {
            _settings.ProcessModeLink = ProcessModeLink.Trim();
        }

        if (InputParser.TryParsePort(ProcessModePort, out var port, out _))
        {
            _settings.ProcessModePort = port;
        }

        _settings.IsProcessModeActive = isActive;
        SettingsService.Save(_settings);
    }

    private void UpdateProcessModePreferences()
    {
        _settings.ProcessModeLink = ProcessModeLink.Trim();

        if (InputParser.TryParsePort(ProcessModePort, out var port, out _))
        {
            _settings.ProcessModePort = port;
        }

        _settings.ProcessModeApplications = ProcessModeApplications.ToList();
    }

    private void UpdateTunnelingPreferences()
    {
        var address = ProxyAddress.Trim();
        if (!string.IsNullOrWhiteSpace(address))
        {
            _settings.ProxyAddress = address;
            _settings.ProxyHistory.RemoveAll(x => x.Equals(address, StringComparison.OrdinalIgnoreCase));
            _settings.ProxyHistory.Insert(0, address);
            _settings.ProxyHistory = _settings.ProxyHistory.Take(10).ToList();
        }

        if (InputParser.TryParsePort(PacPort, out var pacPort, out _))
        {
            _settings.PacPort = pacPort;
            _settings.PacPortHistory.Remove(pacPort);
            _settings.PacPortHistory.Insert(0, pacPort);
            _settings.PacPortHistory = _settings.PacPortHistory.Take(10).ToList();
        }

        _settings.SelectedListIds = _selectedListIds.ToList();
        _settings.CustomDomains = CustomDomains.ToList();
        _settings.CustomIps = CustomIps.ToList();
        _settings.RouteAllTrafficThroughProxy = RouteAllTrafficThroughProxy;
    }

    private void UpdateAppSettingsPreferences()
    {
        _settings.StartWithWindows = StartWithWindows;
        _settings.StartProxyWithApp = StartProxyWithApp;
        _settings.StartProcessModeWithApp = StartProcessModeWithApp;
        _settings.StartMinimizedToTray = StartMinimizedToTray;
        _settings.NotifyOnMinimizeToTray = NotifyOnMinimizeToTray;
        _settings.UpdateListsOnStartup = UpdateListsOnStartup;
    }

    private void UpdateFooter()
    {
        FooterRight = WindowsProxySettings.IsPacEnabled(out _) ? "PAC: вкл" : "PAC: выкл";
    }
}
