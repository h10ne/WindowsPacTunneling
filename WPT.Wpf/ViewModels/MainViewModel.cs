using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows.Threading;
using WPT.Core;
using WPT.Core.Models;
using WPT.Core.Services;
using WPT.Core.Services.Bypass;
using MessageBox = System.Windows.MessageBox;
using MessageBoxButton = System.Windows.MessageBoxButton;
using MessageBoxImage = System.Windows.MessageBoxImage;
using MessageBoxResult = System.Windows.MessageBoxResult;

namespace WPT.Wpf.ViewModels;

public sealed class MainViewModel : ViewModelBase
{
    private readonly DomainListService _domainListService = new();
    private readonly PacHttpServer _pacHttpServer = new();
    private readonly LocalProxyService _localProxyService = new();
    private readonly ProcessModeService _processModeService = new();
    private readonly BypassService _bypassService = new();
    private readonly AppSettings _settings = SettingsService.Load();
    private readonly HashSet<string> _selectedListIds = new(StringComparer.OrdinalIgnoreCase);
    private readonly DispatcherTimer _dailyUpdateTimer;
    private readonly DispatcherTimer _updateCheckTimer;
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
    private bool _isProcessModeUdpSupported = true;
    private bool _isProcessModeHealthChecking;
    private int? _processModePingMs;

    private string _proxyAddress = string.Empty;
    private string _pacPort = string.Empty;
    private string _proxyLink = string.Empty;
    private string _proxyConfigName = string.Empty;
    private string _proxyConfigSaveNotice = string.Empty;
    private SavedProxyConfigItem? _selectedSavedProxyConfig;
    private bool _isSyncingProxyConfig;
    private string _localPort = string.Empty;
    private string _processModeLink = string.Empty;
    private string _processModeAmneziaEndpoint = string.Empty;
    private string _processModeAmneziaSourceName = string.Empty;
    private bool _hasProcessModeAmneziaConfig;
    private ProcessModeConnectionType _processModeConnectionType;
    private string _processModePort = string.Empty;
    private string _newProcessModeApp = string.Empty;
    private string _processModeStatus = "Process Mode: остановлен";
    private string _newDomain = string.Empty;
    private string _newIp = string.Empty;
    private string _footerLog = string.Empty;
    private string _footerRight = "PAC: выкл";
    private string _footerProxyStatus = "Прокси: остановлен";
    private string _footerBypassStatus = "Обход: остановлен";
    private string _footerProcessModeStatus = "PM: остановлен";
    private string _proxyState = "Прокси остановлен";
    private int _selectedSection;
    private int _settingsTabIndex;
    private bool _isBusy;
    private bool _startWithWindows;
    private bool _startProxyWithApp;
    private bool _startProcessModeWithApp;
    private bool _startBypassWithApp;
    private bool _runAsAdministrator;
    private bool _startMinimizedToTray;
    private bool _zapretUpdateAvailable;
    private string _zapretUpdateStatus = "Нажмите «Проверить обновления», чтобы узнать актуальность zapret.";
    private bool _singBoxUpdateAvailable;
    private string _singBoxUpdateStatus = "Нажмите «Проверить обновления», чтобы узнать актуальность sing-box.";
    private bool _isAppUpdateAvailable;
    private string _appUpdateStatus = string.Empty;
    private string _latestAppVersionLabel = string.Empty;
    private readonly string _appVersionLabel = AppVersion.CurrentLabel;
    private bool _notifyOnMinimizeToTray;
    private bool _updateListsOnStartup;
    private bool _routeAllTrafficThroughProxy;
    private bool _showRussiaInsideRestrictionHint;
    private bool _bypassEnableZapret = true;
    private bool _bypassEnableTelegram = true;
    private string _bypassStatus = "Обход: остановлен";
    private string _bypassActiveStrategy = string.Empty;
    private string? _selectedZapretStrategy;
    private string _telegramProxyLink = string.Empty;
    private string _tgWsProxyPort = "1443";
    private string _bypassInfoText = string.Empty;
    private bool _telegramLinkCopiedToClipboard;
    private bool _isBypassProbingStrategy;
    private int _bypassProbeCurrent;
    private int _bypassProbeTotal;
    private bool _bypassProbeFromStart;
    private bool _isRefreshingZapretStrategies;
    private ServiceListDefinition? _selectedListToAdd;

    public MainViewModel()
    {
        SelectedLists = [];
        CustomDomains = [];
        CustomIps = [];
        AvailableZapretStrategies = [];
        ProcessModeApplications = [];
        ProcessModeConnectionTypes =
        [
            new ProcessModeConnectionTypeOption
            {
                Value = ProcessModeConnectionType.Shadowsocks,
                DisplayName = "Shadowsocks (ss://)"
            },
            new ProcessModeConnectionTypeOption
            {
                Value = ProcessModeConnectionType.Amnezia,
                DisplayName = "Amnezia (.conf)"
            }
        ];
        ProxyHistory = [];
        PacPortHistory = [];
        SavedProxyConfigs = [];
        AvailableLists = [.. ServiceListDefinition.All];

        ApplyCommand = new RelayCommand(async () => await ApplyAsync(), () => !IsBusy);
        ShowPacCommand = new RelayCommand(async () => await ShowPacAsync(), () => !IsBusy);
        DisablePacCommand = new RelayCommand(DisablePac, () => !IsBusy && IsPacActive);
        ToggleProxyCommand = new RelayCommand(async () => await ToggleProxyAsync(), () => !IsBusy);
        SaveProxyConfigCommand = new RelayCommand(SaveProxyConfig, () => IsProxyEditingEnabled);
        DeleteSavedProxyConfigCommand = new RelayCommand(
            DeleteSavedProxyConfig,
            parameter => IsProxyEditingEnabled && parameter is SavedProxyConfigItem);
        ToggleBypassCommand = new RelayCommand(async () => await ToggleBypassAsync(), () => CanToggleBypass);
        ProbeBypassStrategyCommand = new RelayCommand(async () => await ProbeBypassStrategyAsync(), () => CanProbeBypassStrategy);
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
        PickAmneziaConfigCommand = new RelayCommand(PickAmneziaConfigFile, () => IsProcessModeEditingEnabled);
        ClearAmneziaConfigCommand = new RelayCommand(ClearAmneziaConfig, () => IsProcessModeEditingEnabled && HasProcessModeAmneziaConfig);
        UpdateListsCommand = new RelayCommand(async () => await UpdateListsAsync());
        SaveSettingsCommand = new RelayCommand(SaveAppSettings);
        OpenDataFolderCommand = new RelayCommand(OpenDataFolder);
        RestartAsAdminCommand = new RelayCommand(RestartAsAdmin, () => IsRestartAsAdminEnabled && !IsBusy);
        CheckZapretUpdateCommand = new RelayCommand(async () => await CheckZapretUpdateAsync(), () => !IsBusy);
        CheckSingBoxUpdateCommand = new RelayCommand(async () => await CheckSingBoxUpdateAsync(), () => !IsBusy);
        CheckAppUpdateCommand = new RelayCommand(async () => await CheckAppUpdateManualAsync(), () => !IsBusy);
        DownloadZapretUpdateCommand = new RelayCommand(async () => await DownloadZapretUpdateAsync(), () => !IsBusy && CanDownloadZapretUpdate);
        DownloadSingBoxUpdateCommand = new RelayCommand(async () => await DownloadSingBoxUpdateAsync(), () => !IsBusy && CanDownloadSingBoxUpdate);
        InstallAppUpdateCommand = new RelayCommand(async () => await InstallAppUpdateAsync(), () => !IsBusy && IsAppUpdateAvailable);
        OpenSettingsForUpdateCommand = new RelayCommand(OpenSettingsForUpdate);
        AppUpdateStatus = $"Текущая версия: {AppVersionLabel}.";

        _domainListService.StatusChanged += (_, message) => SetFooterLog(message);

        _dailyUpdateTimer = new DispatcherTimer { Interval = TimeSpan.FromHours(1) };
        _dailyUpdateTimer.Tick += async (_, _) =>
        {
            try
            {
                await _domainListService.EnsureUpdatedAsync();
            }
            catch (Exception ex)
            {
                AppLog.Error(ex, "Ошибка фонового обновления списков доменов");
            }
        };
        _dailyUpdateTimer.Start();

        _updateCheckTimer = new DispatcherTimer { Interval = TimeSpan.FromHours(1) };
        _updateCheckTimer.Tick += async (_, _) =>
        {
            try
            {
                await CheckAllUpdatesAsync(silent: true);
            }
            catch (Exception ex)
            {
                AppLog.Error(ex, "Ошибка фоновой проверки обновлений");
            }
        };
        _updateCheckTimer.Start();

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

    public IReadOnlyList<ProcessModeConnectionTypeOption> ProcessModeConnectionTypes { get; }

    public ObservableCollection<string> ProxyHistory { get; }

    public ObservableCollection<string> PacPortHistory { get; }

    public ObservableCollection<SavedProxyConfigItem> SavedProxyConfigs { get; }

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

    public string ProxyConfigName
    {
        get => _proxyConfigName;
        set => SetProperty(ref _proxyConfigName, value);
    }

    public string ProxyConfigSaveNotice
    {
        get => _proxyConfigSaveNotice;
        private set
        {
            if (SetProperty(ref _proxyConfigSaveNotice, value))
            {
                OnPropertyChanged(nameof(HasProxyConfigSaveNotice));
            }
        }
    }

    public bool HasProxyConfigSaveNotice => !string.IsNullOrEmpty(_proxyConfigSaveNotice);

    public void ClearProxyConfigSaveNotice() => ProxyConfigSaveNotice = string.Empty;

    public SavedProxyConfigItem? SelectedSavedProxyConfig
    {
        get => _selectedSavedProxyConfig;
        set
        {
            if (!SetProperty(ref _selectedSavedProxyConfig, value))
            {
                return;
            }

            if (!_isSyncingProxyConfig)
            {
                ClearProxyConfigSaveNotice();
            }

            if (_isSyncingProxyConfig || value == null)
            {
                return;
            }

            ApplySelectedProxyConfig(value);
        }
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

    public string ProcessModeAmneziaEndpoint
    {
        get => _processModeAmneziaEndpoint;
        private set => SetProperty(ref _processModeAmneziaEndpoint, value);
    }

    public string ProcessModeAmneziaSourceName
    {
        get => _processModeAmneziaSourceName;
        private set => SetProperty(ref _processModeAmneziaSourceName, value);
    }

    public bool HasProcessModeAmneziaConfig
    {
        get => _hasProcessModeAmneziaConfig;
        private set
        {
            if (SetProperty(ref _hasProcessModeAmneziaConfig, value))
            {
                OnPropertyChanged(nameof(IsProcessModeAmneziaDropHintVisible));
            }
        }
    }

    public bool IsProcessModeAmneziaDropHintVisible => !HasProcessModeAmneziaConfig;

    public ProcessModeConnectionType ProcessModeConnectionType
    {
        get => _processModeConnectionType;
        set
        {
            if (SetProperty(ref _processModeConnectionType, value))
            {
                OnPropertyChanged(nameof(IsProcessModeSsSelected));
                OnPropertyChanged(nameof(IsProcessModeAmneziaSelected));
            }
        }
    }

    public bool IsProcessModeSsSelected => ProcessModeConnectionType == ProcessModeConnectionType.Shadowsocks;

    public bool IsProcessModeAmneziaSelected => ProcessModeConnectionType == ProcessModeConnectionType.Amnezia;

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
            OnPropertyChanged(nameof(IsBypassPage));
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

    public bool IsBypassPage
    {
        get => SelectedSection == 2;
        set { if (value) SelectedSection = 2; }
    }

    public bool IsSettingsPage
    {
        get => SelectedSection == 3;
        set { if (value) SelectedSection = 3; }
    }

    public int SettingsTabIndex
    {
        get => _settingsTabIndex;
        set => SetProperty(ref _settingsTabIndex, value);
    }

    public string FooterLog
    {
        get => _footerLog;
        set => SetProperty(ref _footerLog, value);
    }

    public string StatusMessage
    {
        get => FooterLog;
        set => FooterLog = value;
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

    public string FooterBypassStatus
    {
        get => _footerBypassStatus;
        set => SetProperty(ref _footerBypassStatus, value);
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
                OnPropertyChanged(nameof(IsBypassEditingEnabled));
                OnPropertyChanged(nameof(IsTgWsProxyPortEditingEnabled));
                NotifyBypassCommandState();
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

    public bool IsBypassRunning => _bypassService.IsZapretRunning || _bypassService.IsTelegramRunning;

    public bool IsBypassZapretRunning => _bypassService.IsZapretRunning;

    public bool IsBypassTelegramRunning => _bypassService.IsTelegramRunning;

    public bool IsBypassProbingStrategy => _isBypassProbingStrategy;

    public bool IsBypassEditingEnabled => !IsBypassRunning && !IsBypassProbingStrategy && !IsBusy;

    public bool IsTgWsProxyPortEditingEnabled => !IsBypassTelegramRunning && !IsBypassProbingStrategy && !IsBusy;

    public string BypassToggleLabel
    {
        get
        {
            if (IsBypassRunning)
            {
                return "Остановить обход";
            }

            if (IsBypassProbingStrategy && _bypassProbeFromStart)
            {
                return _bypassProbeTotal > 0
                    ? $"Подбор стратегии {_bypassProbeCurrent}/{_bypassProbeTotal}"
                    : "Подбор стратегии...";
            }

            return "Запустить обход";
        }
    }

    public string BypassProbeLabel
    {
        get
        {
            if (IsBypassProbingStrategy)
            {
                return _bypassProbeTotal > 0
                    ? $"Подбор {_bypassProbeCurrent}/{_bypassProbeTotal}"
                    : "Подбор стратегии...";
            }

            return "Подобрать стратегию";
        }
    }

    public bool CanStartBypass =>
        !IsBusy
        && !IsBypassRunning
        && !IsBypassProbingStrategy
        && (BypassEnableZapret || BypassEnableTelegram);

    public bool CanToggleBypass => !IsBypassProbingStrategy && !IsBusy && (IsBypassRunning || CanStartBypass);

    public bool CanProbeBypassStrategy => BypassEnableZapret && !IsBusy && !IsBypassProbingStrategy;

    public bool BypassEnableZapret
    {
        get => _bypassEnableZapret;
        set
        {
            if (SetProperty(ref _bypassEnableZapret, value))
            {
                NotifyBypassCommandState();
                UpdateBypassInfoText();
            }
        }
    }

    public bool BypassEnableTelegram
    {
        get => _bypassEnableTelegram;
        set
        {
            if (SetProperty(ref _bypassEnableTelegram, value))
            {
                NotifyBypassCommandState();
                UpdateBypassInfoText();
            }
        }
    }

    public string BypassStatus
    {
        get => _bypassStatus;
        set => SetProperty(ref _bypassStatus, value);
    }

    public string BypassActiveStrategy
    {
        get => _bypassActiveStrategy;
        set => SetProperty(ref _bypassActiveStrategy, value);
    }

    public ObservableCollection<string> AvailableZapretStrategies { get; }

    public string? SelectedZapretStrategy
    {
        get => _selectedZapretStrategy;
        set
        {
            var normalized = string.IsNullOrWhiteSpace(value) ? null : value;
            if (string.Equals(_selectedZapretStrategy, normalized, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            _selectedZapretStrategy = normalized;
            BypassActiveStrategy = normalized ?? string.Empty;
            OnPropertyChanged();

            if (_isRefreshingZapretStrategies)
            {
                return;
            }

            _settings.SavedZapretStrategy = normalized;
            SaveBypassSettings(IsBypassRunning);
        }
    }

    public string TgWsProxyPort
    {
        get => _tgWsProxyPort;
        set => SetProperty(ref _tgWsProxyPort, value);
    }

    public string BypassInfoText
    {
        get => _bypassInfoText;
        private set
        {
            if (SetProperty(ref _bypassInfoText, value))
            {
                OnPropertyChanged(nameof(IsBypassInfoTextVisible));
            }
        }
    }

    public bool IsBypassInfoTextVisible => !string.IsNullOrWhiteSpace(BypassInfoText);

    public bool HasZapretStrategy => !string.IsNullOrWhiteSpace(_settings.SavedZapretStrategy);

    public string TelegramProxyLink
    {
        get => _telegramProxyLink;
        set
        {
            if (SetProperty(ref _telegramProxyLink, value))
            {
                RelayCommand.RaiseAllCanExecuteChanged();
            }
        }
    }

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

    public bool RunAsAdministrator
    {
        get => _runAsAdministrator;
        set
        {
            if (SetProperty(ref _runAsAdministrator, value) && !value)
            {
                StartBypassWithApp = false;
            }

            OnPropertyChanged(nameof(IsStartBypassWithAppEnabled));
        }
    }

    public bool StartBypassWithApp
    {
        get => _startBypassWithApp;
        set => SetProperty(ref _startBypassWithApp, value);
    }

    public bool IsStartBypassWithAppEnabled => RunAsAdministrator;

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

    public bool IsRestartAsAdminEnabled => !AdminHelper.IsRunningAsAdmin();

    public string ZapretUpdateStatus
    {
        get => _zapretUpdateStatus;
        private set => SetProperty(ref _zapretUpdateStatus, value);
    }

    public bool CanDownloadZapretUpdate
    {
        get => _zapretUpdateAvailable;
        private set
        {
            if (SetProperty(ref _zapretUpdateAvailable, value))
            {
                OnPropertyChanged(nameof(IsAnyUpdateAvailable));
                RelayCommand.RaiseAllCanExecuteChanged();
            }
        }
    }

    public string SingBoxUpdateStatus
    {
        get => _singBoxUpdateStatus;
        private set => SetProperty(ref _singBoxUpdateStatus, value);
    }

    public bool CanDownloadSingBoxUpdate
    {
        get => _singBoxUpdateAvailable;
        private set
        {
            if (SetProperty(ref _singBoxUpdateAvailable, value))
            {
                OnPropertyChanged(nameof(IsAnyUpdateAvailable));
                RelayCommand.RaiseAllCanExecuteChanged();
            }
        }
    }

    public string AppVersionLabel => _appVersionLabel;

    public string AppUpdateStatus
    {
        get => _appUpdateStatus;
        private set => SetProperty(ref _appUpdateStatus, value);
    }

    public bool IsAppUpdateAvailable
    {
        get => _isAppUpdateAvailable;
        private set
        {
            if (SetProperty(ref _isAppUpdateAvailable, value))
            {
                OnPropertyChanged(nameof(IsAnyUpdateAvailable));
                RelayCommand.RaiseAllCanExecuteChanged();
            }
        }
    }

    public bool IsAnyUpdateAvailable =>
        IsAppUpdateAvailable || CanDownloadZapretUpdate || CanDownloadSingBoxUpdate;

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

    public RelayCommand SaveProxyConfigCommand { get; }

    public RelayCommand DeleteSavedProxyConfigCommand { get; }

    public RelayCommand ToggleBypassCommand { get; }

    public RelayCommand ProbeBypassStrategyCommand { get; }

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

    public RelayCommand PickAmneziaConfigCommand { get; }

    public RelayCommand ClearAmneziaConfigCommand { get; }

    public RelayCommand UpdateListsCommand { get; }

    public RelayCommand SaveSettingsCommand { get; }

    public RelayCommand OpenDataFolderCommand { get; }

    public RelayCommand RestartAsAdminCommand { get; }

    public RelayCommand CheckZapretUpdateCommand { get; }

    public RelayCommand CheckSingBoxUpdateCommand { get; }

    public RelayCommand CheckAppUpdateCommand { get; }

    public RelayCommand DownloadZapretUpdateCommand { get; }

    public RelayCommand DownloadSingBoxUpdateCommand { get; }

    public RelayCommand InstallAppUpdateCommand { get; }

    public RelayCommand OpenSettingsForUpdateCommand { get; }

    public async Task InitializeAsync()
    {
        IsBusy = true;
        AppLog.Info($"Инициализация UI (admin={AdminHelper.IsRunningAsAdmin()})");
        var updateCheckTask = CheckAllUpdatesAsync(silent: true);
        SetFooterLog("Инициализация приложения...");

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

            if (AppBranding.IsProcessModeUiVisible
                && _settings.IsProcessModeActive
                && HasProcessModeConfig()
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
            else if (AppBranding.IsProcessModeUiVisible
                && StartProcessModeWithApp
                && HasProcessModeConfig()
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
                SetFooterLog("PAC был активен. Нажмите «Применить» для повторной активации.");
            }

            if (StartBypassWithApp && _settings.IsBypassActive)
            {
                await RestoreBypassAsync(silent: true);
            }
            else
            {
                await _bypassService.TryAdoptExistingAsync(_settings.SavedZapretStrategy);
                UpdateBypassUi();
            }

            if (_settings.IsBypassActive && !StartBypassWithApp)
            {
                SetFooterLog("Обход был активен. Нажмите «Запустить» для повторной активации.");
            }
        }
        finally
        {
            try
            {
                await updateCheckTask;
            }
            catch (Exception ex)
            {
                AppLog.Error(ex, "Ошибка проверки обновлений при запуске");
            }

            IsBusy = false;
            RefreshPacState();
            if (FooterLog == "Инициализация приложения...")
            {
                SetFooterLog("Готово");
            }
        }
    }

    public void SaveUiState()
    {
        UpdateTunnelingPreferences();
        UpdateProcessModePreferences();
        SaveProxySettings(_localProxyService.IsRunning);
        SaveProcessModeSettings(_processModeService.IsRunning);
        SaveBypassSettings(IsBypassRunning);
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
            StopBypassOnExit();
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

    public void StopBypassOnExit()
    {
        try
        {
            _bypassService.StopAsync(BypassEnableZapret, BypassEnableTelegram).ConfigureAwait(false).GetAwaiter().GetResult();
            SaveBypassSettings(isActive: false);
            UpdateBypassUi();
        }
        catch
        {
        }
    }

    private bool _isShutdown;

    public void Shutdown()
    {
        if (_isShutdown)
        {
            return;
        }

        _isShutdown = true;
        StopTimers();
        StopServicesForUpdate();

        _domainListService.Dispose();
        _pacHttpServer.Dispose();
        _localProxyService.Dispose();
        _processModeService.Dispose();
        WaitShutdownTask(_bypassService.DisposeAsync().AsTask(), TimeSpan.FromSeconds(15));
    }

    private void StopTimers()
    {
        _dailyUpdateTimer.Stop();
        _proxyHealthTimer.Stop();
        _processModeHealthTimer.Stop();
    }

    private static void WaitShutdownTask(Task task, TimeSpan timeout)
    {
        try
        {
            if (!task.Wait(timeout))
            {
                AppLog.Warning("Таймаут остановки фоновой задачи при выходе из приложения");
            }
        }
        catch (Exception ex)
        {
            AppLog.Debug(ex, "Ошибка остановки фоновой задачи при выходе из приложения");
        }
    }

    private void StopServicesForUpdate()
    {
        try
        {
            _pacHttpServer.Stop();
            StopLocalProxyOnExit();
            StopProcessModeOnExit();
            StopBypassOnExit();
            SaveUiState();
        }
        catch
        {
        }
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
        RunAsAdministrator = _settings.RunAsAdministrator;
        StartProxyWithApp = _settings.StartProxyWithApp;
        StartProcessModeWithApp = AppBranding.IsProcessModeUiVisible && _settings.StartProcessModeWithApp;
        StartBypassWithApp = _settings.RunAsAdministrator && _settings.StartBypassWithApp;
        StartMinimizedToTray = _settings.StartMinimizedToTray;
        NotifyOnMinimizeToTray = _settings.NotifyOnMinimizeToTray;
        UpdateListsOnStartup = _settings.UpdateListsOnStartup;
        RouteAllTrafficThroughProxy = _settings.RouteAllTrafficThroughProxy;
        LoadSavedProxyConfigs();
        if (SelectedSavedProxyConfig == null)
        {
            ProxyLink = _settings.ProxyLink;
        }
        LocalPort = _settings.LocalProxyPort.ToString();
        if (InputParser.TryParsePort(LocalPort, out var localPort, out _))
        {
            _localProxyService.Prepare(localPort);
        }

        BypassEnableZapret = _settings.BypassEnableZapret;
        BypassEnableTelegram = _settings.BypassEnableTelegram;
        RefreshAvailableZapretStrategies();
        SyncSelectedZapretStrategyFromSettings();
        TgWsProxyPort = _settings.TgWsProxyPort.ToString();
        TelegramProxyLink = BuildTelegramProxyLinkPreview(_settings.TgWsProxyPort, _settings.TgWsProxySecret);
        TryAdoptBypassState();
        UpdateBypassInfoText();
        NotifyBypassCommandState();
        RefreshZapretUpdateStatusHint();
        RefreshSingBoxUpdateStatusHint();

        ProcessModeLink = _settings.ProcessModeLink;
        ProcessModeConnectionType = _settings.ProcessModeConnectionType;
        RefreshProcessModeAmneziaConfigState(_settings.ProcessModeAmneziaSourceName);
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

        if (HasProcessModeConfig()
            && !TryValidateProcessModeConnection(out var parseError))
        {
            MessageBox.Show(parseError, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        _processModeService.Prepare(port);
        SaveProcessModeSettings(isActive: _processModeService.IsRunning);
        SetFooterLog("Настройки Process Mode сохранены");
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

        if (!TryValidateProcessModeConnection(out var parseError))
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

        if (!TryResolveProcessModePacConflict(silent, localPort))
        {
            return;
        }

        IsBusy = true;
        SetFooterLog("Восстановление Process Mode...");

        try
        {
            var progress = new Progress<string>(message =>
            {
                ProcessModeStatus = message;
                SetFooterLog(message);
            });
            await _processModeService.TryRestoreAsync(
                ProcessModeConnectionType,
                TryGetProcessModeShadowsocksProfile(),
                TryGetProcessModeAmneziaConfig(),
                localPort,
                ProcessModeApplications.ToList(),
                progress,
                CancellationToken.None);

            SaveProcessModeSettings(isActive: true);
            UpdateProcessModeUi();
            _ = RefreshProcessModeHealthAsync(showCheckingState: false);
            SetFooterLog($"Process Mode восстановлен: {_processModeService.LocalProxyAddress}");
        }
        catch (Exception ex)
        {
            AppLog.Error(ex, "Ошибка восстановления Process Mode");
            UpdateProcessModeUi();
            SetFooterLog($"Ошибка восстановления Process Mode: {ex.Message}");
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

        if (!TryValidateProcessModeConnection(out var parseError))
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

        if (!TryResolveProcessModePacConflict(silent, localPort))
        {
            return;
        }

        IsBusy = true;
        SetFooterLog("Запуск Process Mode...");

        try
        {
            var progress = new Progress<string>(message =>
            {
                ProcessModeStatus = message;
                SetFooterLog(message);
            });
            await _processModeService.StartAsync(
                ProcessModeConnectionType,
                TryGetProcessModeShadowsocksProfile(),
                TryGetProcessModeAmneziaConfig(),
                localPort,
                ProcessModeApplications.ToList(),
                progress,
                CancellationToken.None);

            SaveProcessModeSettings(isActive: true);
            UpdateProcessModeUi();
            _ = RefreshProcessModeHealthAsync(showCheckingState: true);
            SetFooterLog($"Process Mode запущен: {_processModeService.LocalProxyAddress}");
        }
        catch (Exception ex)
        {
            AppLog.Error(ex, "Ошибка запуска Process Mode");
            UpdateProcessModeUi();
            SetFooterLog($"Ошибка запуска Process Mode: {ex.Message}");
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
        SetFooterLog("Process Mode остановлен");
    }

    private bool TryResolveProcessModePacConflict(bool silent, int processModePort)
    {
        if (!ProcessModePacConflict.ShouldWarn)
        {
            return true;
        }

        var message = ProcessModePacConflict.BuildWarningMessage(
            ProxyAddress,
            processModePort,
            _localProxyService.IsRunning);

        if (silent)
        {
            SetFooterLog("PAC включён — Discord идёт через системный прокси, не Redirector");
            ProcessModeStatus = FooterLog;
            return true;
        }

        var result = MessageBox.Show(
            message,
            "PAC и Process Mode",
            MessageBoxButton.YesNoCancel,
            MessageBoxImage.Warning);

        if (result == MessageBoxResult.Yes)
        {
            DisablePac();
            return true;
        }

        return result == MessageBoxResult.No;
    }

    private void UpdateProcessModeUi()
    {
        var isRunning = _processModeService.IsRunning;
        var address = _processModeService.LocalProxyAddress;
        var tunnelLabel = _processModeService.ActiveConnectionType == ProcessModeConnectionType.Amnezia
            ? "AmneziaWG"
            : "ss";

        if (!isRunning)
        {
            FooterProcessModeStatus = "PM: остановлен";
            ProcessModeStatus = "Process Mode: остановлен";
        }
        else if (_isProcessModeHealthChecking && _processModePingMs == null)
        {
            FooterProcessModeStatus = "PM: проверка";
            ProcessModeStatus =
                $"Process Mode: работает · {address} · {tunnelLabel} · Redirector · {ProcessModeApplications.Count} прилож.";
        }
        else if (_isProcessModeHealthy)
        {
            FooterProcessModeStatus = $"PM: SOCKS · {_processModePingMs} мс";
            ProcessModeStatus =
                $"Process Mode: SOCKS ok · {address} · {tunnelLabel} · Redirector · {ProcessModeApplications.Count} прилож. · {_processModePingMs} мс";
        }
        else if (_isProcessModeUnreachable && _processModePingMs != null && !_isProcessModeUdpSupported)
        {
            FooterProcessModeStatus = "PM: UDP недоступен";
            ProcessModeStatus =
                $"Process Mode: TCP ok, UDP нет · {address} · {tunnelLabel} · Redirector · {ProcessModeApplications.Count} прилож.";
        }
        else if (_isProcessModeUnreachable)
        {
            FooterProcessModeStatus = "PM: нет доступа";
            ProcessModeStatus =
                $"Process Mode: нет доступа · {address} · {tunnelLabel} · Redirector · {ProcessModeApplications.Count} прилож.";
        }
        else
        {
            var apps = ProcessModeApplications.Count;
            FooterProcessModeStatus = "PM: проверка";
            ProcessModeStatus =
                $"Process Mode: проверка · {address} · {tunnelLabel} · Redirector · {apps} прилож.";
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

        if (!ProxyLinkParser.TryParse(ResolveProxyLinkForStart(), out var profile, out var parseError))
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
        SetFooterLog("Запуск локального прокси...");

        try
        {
            var progress = new Progress<string>(message =>
            {
                ProxyState = message;
                SetFooterLog(message);
            });
            await _localProxyService.StartAsync(profile, localPort, progress, CancellationToken.None);

            SaveProxySettings(isActive: true);
            UpdateProxyUi();
            UpdateFooter();
            _ = RefreshProxyHealthAsync();

            SetFooterLog($"Локальный прокси запущен: {_localProxyService.LocalProxyAddress}");
        }
        catch (Exception ex)
        {
            AppLog.Error(ex, "Ошибка запуска локального прокси");
            ResetProxyHealth();
            UpdateProxyUi();
            SetFooterLog($"Ошибка запуска прокси: {ex.Message}");
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
        SetFooterLog("Локальный прокси остановлен");
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
        RelayCommand.RaiseAllCanExecuteChanged();
    }

    private void LoadSavedProxyConfigs()
    {
        SavedProxyConfigs.Clear();

        foreach (var config in _settings.SavedProxyConfigs)
        {
            SavedProxyConfigs.Add(SavedProxyConfigItem.FromModel(config));
        }

        var selected = SavedProxyConfigs.FirstOrDefault(x => x.Id == _settings.SelectedProxyConfigId)
            ?? SavedProxyConfigs.FirstOrDefault();

        _isSyncingProxyConfig = true;
        if (selected == null)
        {
            SelectedSavedProxyConfig = null;
            _isSyncingProxyConfig = false;
            return;
        }

        SelectedSavedProxyConfig = selected;
        _isSyncingProxyConfig = false;
        ApplySelectedProxyConfig(selected, updateSelection: false);
    }

    private void ApplySelectedProxyConfig(SavedProxyConfigItem config, bool updateSelection = true)
    {
        _isSyncingProxyConfig = true;
        ProxyConfigName = config.Name;
        ProxyLink = config.Link;
        _settings.SelectedProxyConfigId = config.Id;
        _settings.ProxyLink = config.Link;

        if (updateSelection && !ReferenceEquals(SelectedSavedProxyConfig, config))
        {
            SelectedSavedProxyConfig = config;
        }

        _isSyncingProxyConfig = false;
    }

    private void SaveProxyConfig()
    {
        var name = ProxyConfigName.Trim();
        var link = ProxyLink.Trim();

        if (string.IsNullOrWhiteSpace(name))
        {
            MessageBox.Show("Укажите название конфигурации.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!ProxyLinkParser.TryParse(link, out var profile, out var parseError))
        {
            MessageBox.Show(parseError, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var existingByName = _settings.SavedProxyConfigs
            .FirstOrDefault(x => x.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

        if (existingByName != null)
        {
            if (existingByName.Link.Equals(link, StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show(
                    "Конфигурация не изменилась.",
                    "Ошибка",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            if (_settings.SavedProxyConfigs.Any(x => x.Id != existingByName.Id
                && x.Link.Equals(link, StringComparison.OrdinalIgnoreCase)))
            {
                MessageBox.Show(
                    "Такая строка подключения уже сохранена.",
                    "Ошибка",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            existingByName.Link = link;
            existingByName.Protocol = profile.Protocol;
            _settings.SelectedProxyConfigId = existingByName.Id;
            _settings.ProxyLink = link;
            SettingsService.Save(_settings);
            LoadSavedProxyConfigs();
            ProxyConfigSaveNotice = $"Конфигурация «{name}» обновлена";
            SetFooterLog($"Конфигурация «{name}» обновлена");
            return;
        }

        if (_settings.SavedProxyConfigs.Any(x => x.Link.Equals(link, StringComparison.OrdinalIgnoreCase)))
        {
            MessageBox.Show(
                "Такая строка подключения уже сохранена.",
                "Ошибка",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        var config = new SavedProxyConfiguration
        {
            Name = name,
            Link = link,
            Protocol = profile.Protocol
        };
        _settings.SavedProxyConfigs.Add(config);

        _settings.SelectedProxyConfigId = config.Id;
        _settings.ProxyLink = link;
        SettingsService.Save(_settings);
        LoadSavedProxyConfigs();
        _isSyncingProxyConfig = true;
        ProxyConfigName = string.Empty;
        ProxyLink = string.Empty;
        _isSyncingProxyConfig = false;
        SetFooterLog($"Конфигурация «{name}» сохранена");
    }

    private string ResolveProxyLinkForStart()
    {
        if (!string.IsNullOrWhiteSpace(SelectedSavedProxyConfig?.Link))
        {
            return SelectedSavedProxyConfig.Link;
        }

        return ProxyLink.Trim();
    }

    private void DeleteSavedProxyConfig(object? parameter)
    {
        if (parameter is not SavedProxyConfigItem item)
        {
            return;
        }

        var model = _settings.SavedProxyConfigs.FirstOrDefault(x => x.Id == item.Id);
        if (model == null)
        {
            return;
        }

        _settings.SavedProxyConfigs.Remove(model);

        if (_settings.SelectedProxyConfigId == item.Id)
        {
            var next = _settings.SavedProxyConfigs.FirstOrDefault();
            _settings.SelectedProxyConfigId = next?.Id;
            _settings.ProxyLink = next?.Link ?? string.Empty;
        }

        SettingsService.Save(_settings);
        LoadSavedProxyConfigs();

        if (SavedProxyConfigs.Count == 0)
        {
            _isSyncingProxyConfig = true;
            ProxyConfigName = string.Empty;
            ProxyLink = string.Empty;
            _isSyncingProxyConfig = false;
        }

        SetFooterLog($"Конфигурация «{item.Name}» удалена");
    }

    private void PersistSelectedProxyConfig()
    {
        if (string.IsNullOrWhiteSpace(ProxyLink))
        {
            return;
        }

        var link = ProxyLink.Trim();
        _settings.ProxyLink = link;

        if (SelectedSavedProxyConfig != null)
        {
            _settings.SelectedProxyConfigId = SelectedSavedProxyConfig.Id;
        }
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
            var result = await ProxyHealthChecker.CheckProcessModeAsync(_processModeService.LocalPort, cancellationToken);
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
        _isProcessModeUdpSupported = result.UdpSupported;
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
        _isProcessModeUdpSupported = true;
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

        SetFooterLog("Обновление списков...");

        try
        {
            await _domainListService.UpdateAllListsAsync();
            SetFooterLog("Списки обновлены");
        }
        catch (Exception ex)
        {
            AppLog.Error(ex, "Ошибка обновления списков доменов");
            SetFooterLog($"Не удалось обновить списки: {ex.Message}");
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
        SetFooterLog("Подготовка PAC-файла...");

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
            AppLog.Error(ex, "Ошибка подготовки PAC-файла");
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
        SetFooterLog("Применение PAC...");

        try
        {
            if (!InputParser.TryParsePort(PacPort, out var pacPort, out var pacPortError))
            {
                SetFooterLog(pacPortError);
                return;
            }

            var pac = await TryBuildPacContentAsync(silent);
            if (pac == null)
            {
                if (silent)
                {
                    SetFooterLog("Не удалось автоматически запустить PAC");
                }

                return;
            }

            await PacStorageService.SaveAsync(pac.Value.Content);

            _pacHttpServer.SetPort(pacPort);
            _pacHttpServer.SetPacContent(pac.Value.Content);
            _pacHttpServer.Restart();

            var pacUrl = _pacHttpServer.GetPacUrl(pac.Value.Hash);
            WindowsProxySettings.EnablePac(pacUrl);
            StartupService.SetEnabled(StartWithWindows, RunAsAdministrator);
            SaveSettings(pac.Value.Hash, isActive: true);

            RefreshPacState();

            SetFooterLog(RouteAllTrafficThroughProxy
                ? $"PAC применён: {pacUrl} · весь трафик через прокси"
                : $"PAC применён: {pacUrl} · {pac.Value.DomainsCount} доменов, {pac.Value.SubnetsCount} подсетей");
        }
        catch (Exception ex)
        {
            AppLog.Error(ex, "Ошибка применения PAC");
            SetFooterLog($"Ошибка применения PAC: {ex.Message}");
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
                SetFooterLog(error);
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
                SetFooterLog("Выберите хотя бы один список или укажите свои домены/IP.");
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
                SetFooterLog("Не найдено доменов или IP для формирования PAC.");
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
            SetFooterLog("PAC отключён");
        }
        catch (Exception ex)
        {
            AppLog.Error(ex, "Ошибка отключения PAC");
            MessageBox.Show(ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void SaveAppSettings()
    {
        try
        {
            UpdateAppSettingsPreferences();
            StartupService.SetEnabled(StartWithWindows, RunAsAdministrator);
            SettingsService.Save(_settings);
            SetFooterLog("Настройки сохранены");
            MessageBox.Show(
                "Настройки сохранены.",
                "Готово",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            AppLog.Error(ex, "Ошибка сохранения настроек");
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

    private void OpenSettingsForUpdate()
    {
        SelectedSection = 3;
        SettingsTabIndex = 1;
    }

    private async Task CheckAllUpdatesAsync(bool silent)
    {
        await Task.WhenAll(
            CheckAppUpdateAsync(silent),
            CheckZapretUpdateAsync(silent),
            CheckSingBoxUpdateAsync(silent));
    }

    private async Task CheckAppUpdateManualAsync()
    {
        IsBusy = true;
        SetFooterLog("Проверка обновлений приложения...");

        try
        {
            await CheckAppUpdateAsync(silent: false);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task CheckAppUpdateAsync(bool silent)
    {
        try
        {
            var result = await AppUpdateService.CheckForUpdateAsync();
            IsAppUpdateAvailable = result.UpdateAvailable;
            _latestAppVersionLabel = result.LatestVersionLabel;
            AppUpdateStatus = result.Message;

            if (!silent)
            {
                SetFooterLog(result.Message);
            }
        }
        catch (Exception ex)
        {
            AppLog.Error(ex, "Не удалось проверить обновления приложения");
            IsAppUpdateAvailable = false;
            AppUpdateStatus = $"Не удалось проверить обновления. Текущая версия: {AppVersionLabel}.";

            if (!silent)
            {
                SetFooterLog($"Не удалось проверить обновления приложения: {ex.Message}");
            }
        }
    }

    private async Task InstallAppUpdateAsync()
    {
        if (!IsAppUpdateAvailable)
        {
            return;
        }

        var latestLabel = string.IsNullOrWhiteSpace(_latestAppVersionLabel)
            ? "новой версии"
            : _latestAppVersionLabel;
        var confirm = MessageBox.Show(
            $"Установить обновление до {latestLabel}? Приложение будет перезапущено.",
            "Обновление",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (confirm != MessageBoxResult.Yes)
        {
            return;
        }

        IsBusy = true;

        try
        {
            SetFooterLog("Скачивание обновления...");

            var progress = new Progress<string>(message =>
            {
                AppUpdateStatus = message;
                SetFooterLog(message);
            });
            var downloadPath = await AppUpdateService.DownloadLatestReleaseAsync(progress, CancellationToken.None);
            var targetPath = AppUpdateService.GetCurrentExecutablePath()
                ?? throw new InvalidOperationException("Не удалось определить путь к приложению.");

            SetFooterLog("Подготовка к обновлению...");
            StopServicesForUpdate();
            AppUpdateService.LaunchUpdaterAndExit(downloadPath, targetPath, _settings.RunAsAdministrator);
            Shutdown();
            System.Windows.Application.Current.Shutdown(0);
        }
        catch (Exception ex)
        {
            AppLog.Error(ex, "Ошибка установки обновления приложения");
            var message = ex.InnerException?.Message ?? ex.Message;
            AppUpdateStatus = $"Ошибка обновления: {message}";
            SetFooterLog(AppUpdateStatus);
            IsBusy = false;
        }
    }

    private void RestartAsAdmin()
    {
        if (AdminHelper.IsRunningAsAdmin())
        {
            MessageBox.Show(
                "Приложение уже запущено от имени администратора.",
                "Готово",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        SaveUiState();
        SetFooterLog("Перезапуск от имени администратора...");
        if (!App.TryRestartElevated())
        {
            System.Windows.Application.Current.Shutdown(0);
            return;
        }
    }

    private async Task CheckZapretUpdateAsync(bool silent = false)
    {
        if (!silent)
        {
            IsBusy = true;
            SetFooterLog("Проверка обновлений zapret...");
        }

        try
        {
            var result = await ZapretInstaller.CheckForUpdateAsync();
            ZapretUpdateStatus = result.Message;
            CanDownloadZapretUpdate = result.UpdateAvailable;

            if (!silent)
            {
                SetFooterLog(result.Message);
            }
        }
        catch (Exception ex)
        {
            AppLog.Error(ex, "Не удалось проверить обновления zapret");
            CanDownloadZapretUpdate = false;
            ZapretUpdateStatus = $"Не удалось проверить обновления: {ex.Message}";

            if (!silent)
            {
                SetFooterLog(ZapretUpdateStatus);
            }
        }
        finally
        {
            if (!silent)
            {
                IsBusy = false;
            }
        }
    }

    private async Task DownloadZapretUpdateAsync()
    {
        if (!CanDownloadZapretUpdate)
        {
            return;
        }

        IsBusy = true;

        try
        {
            if (!AdminHelper.IsRunningAsAdmin())
            {
                const string adminRequired =
                    "Обновление zapret требует запуска WPT от имени администратора (драйвер WinDivert).";
                ZapretUpdateStatus = adminRequired;
                SetFooterLog(adminRequired);
                return;
            }

            if (IsBypassZapretRunning || ZapretInstaller.HasRunningProcesses())
            {
                SetFooterLog("Остановка обхода zapret...");
                await _bypassService.StopAsync(stopZapret: true, stopTelegram: false);
                SaveBypassSettings(isActive: IsBypassRunning);
                UpdateBypassUi();
            }

            SetFooterLog("Скачивание обновления zapret...");

            var progress = new Progress<string>(message =>
            {
                ZapretUpdateStatus = message;
                SetFooterLog(message);
            });
            await ZapretInstaller.InstallOrUpdateAsync(progress, CancellationToken.None);
            CanDownloadZapretUpdate = false;
            var version = ZapretInstaller.GetInstalledVersion();
            _settings.SavedZapretStrategy = null;
            RefreshAvailableZapretStrategies();
            SyncSelectedZapretStrategyFromSettings();
            SettingsService.Save(_settings);
            ZapretUpdateStatus = string.IsNullOrWhiteSpace(version)
                ? "Zapret успешно установлен."
                : $"Zapret обновлён до версии {version}.";
            SetFooterLog(ZapretUpdateStatus);
            NotifyBypassCommandState();
        }
        catch (Exception ex)
        {
            AppLog.Error(ex, "Ошибка обновления zapret");
            var message = ex.InnerException?.Message ?? ex.Message;
            ZapretUpdateStatus = $"Ошибка обновления: {message}";
            SetFooterLog(ZapretUpdateStatus);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void RefreshZapretUpdateStatusHint()
    {
        if (!ZapretInstaller.IsInstalled())
        {
            ZapretUpdateStatus = "Zapret не установлен.";
            CanDownloadZapretUpdate = false;
            return;
        }

        var version = ZapretInstaller.GetInstalledVersion();
        ZapretUpdateStatus = string.IsNullOrWhiteSpace(version)
            ? "Zapret установлен. Версия неизвестна — проверьте обновления."
            : $"Установлена версия {version}.";
        CanDownloadZapretUpdate = false;
    }

    private async Task CheckSingBoxUpdateAsync(bool silent = false)
    {
        if (!silent)
        {
            IsBusy = true;
            SetFooterLog("Проверка обновлений sing-box...");
        }

        try
        {
            var result = await SingBoxInstaller.CheckForUpdateAsync();
            SingBoxUpdateStatus = result.Message;
            CanDownloadSingBoxUpdate = result.UpdateAvailable;

            if (!silent)
            {
                SetFooterLog(result.Message);
            }
        }
        catch (Exception ex)
        {
            AppLog.Error(ex, "Не удалось проверить обновления sing-box");
            CanDownloadSingBoxUpdate = false;
            SingBoxUpdateStatus = $"Не удалось проверить обновления: {ex.Message}";

            if (!silent)
            {
                SetFooterLog(SingBoxUpdateStatus);
            }
        }
        finally
        {
            if (!silent)
            {
                IsBusy = false;
            }
        }
    }

    private async Task DownloadSingBoxUpdateAsync()
    {
        if (!CanDownloadSingBoxUpdate)
        {
            return;
        }

        IsBusy = true;

        try
        {
            if (_localProxyService.IsRunning || SingBoxInstaller.HasRunningProcesses())
            {
                SetFooterLog("Остановка локального прокси...");
                _localProxyService.Stop();
                SaveProxySettings(isActive: false);
                UpdateProxyUi();
            }

            if (_processModeService.IsRunning)
            {
                SetFooterLog("Остановка process mode...");
                StopProcessMode();
                SaveProcessModeSettings(isActive: false);
                UpdateProcessModeUi();
            }

            SingBoxInstaller.StopRunningProcesses();
            SetFooterLog("Скачивание обновления sing-box...");

            var progress = new Progress<string>(message =>
            {
                SingBoxUpdateStatus = message;
                SetFooterLog(message);
            });
            await SingBoxInstaller.InstallOrUpdateAsync(progress, CancellationToken.None);
            CanDownloadSingBoxUpdate = false;
            var version = SingBoxInstaller.GetInstalledVersion();
            SingBoxUpdateStatus = string.IsNullOrWhiteSpace(version)
                ? "Sing-box успешно установлен."
                : $"Sing-box обновлён до версии {version}.";
            SetFooterLog(SingBoxUpdateStatus);
        }
        catch (Exception ex)
        {
            AppLog.Error(ex, "Ошибка обновления sing-box");
            var message = ex.InnerException?.Message ?? ex.Message;
            SingBoxUpdateStatus = $"Ошибка обновления: {message}";
            SetFooterLog(SingBoxUpdateStatus);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void RefreshSingBoxUpdateStatusHint()
    {
        if (!SingBoxInstaller.IsInstalled())
        {
            SingBoxUpdateStatus = "Sing-box не установлен.";
            CanDownloadSingBoxUpdate = false;
            return;
        }

        var version = SingBoxInstaller.GetInstalledVersion();
        SingBoxUpdateStatus = string.IsNullOrWhiteSpace(version)
            ? "Sing-box установлен. Версия неизвестна — проверьте обновления."
            : $"Установлена версия {version}.";
        CanDownloadSingBoxUpdate = false;
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
            PersistSelectedProxyConfig();
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

        if (!string.IsNullOrWhiteSpace(ProcessModeAmneziaSourceName))
        {
            _settings.ProcessModeAmneziaSourceName = ProcessModeAmneziaSourceName;
        }

        _settings.ProcessModeConnectionType = ProcessModeConnectionType;

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
        _settings.ProcessModeAmneziaSourceName = ProcessModeAmneziaSourceName;
        _settings.ProcessModeConnectionType = ProcessModeConnectionType;

        if (InputParser.TryParsePort(ProcessModePort, out var port, out _))
        {
            _settings.ProcessModePort = port;
        }

        _settings.ProcessModeApplications = ProcessModeApplications.ToList();
    }

    private bool HasProcessModeConfig() => ProcessModeConnectionType switch
    {
        ProcessModeConnectionType.Amnezia => HasProcessModeAmneziaConfig,
        _ => !string.IsNullOrWhiteSpace(ProcessModeLink)
    };

    private bool TryValidateProcessModeConnection(out string error)
    {
        error = string.Empty;

        return ProcessModeConnectionType switch
        {
            ProcessModeConnectionType.Amnezia => AmneziaConfigStorage.TryReadStored(out _, out _, out error),
            _ => ProxyLinkParser.TryParse(ProcessModeLink, out _, out error)
        };
    }

    private ProxyProfile? TryGetProcessModeShadowsocksProfile()
    {
        if (ProcessModeConnectionType != ProcessModeConnectionType.Shadowsocks)
        {
            return null;
        }

        return ProxyLinkParser.TryParse(ProcessModeLink, out var profile, out _)
            ? profile
            : null;
    }

    private string? TryGetProcessModeAmneziaConfig()
    {
        if (ProcessModeConnectionType != ProcessModeConnectionType.Amnezia)
        {
            return null;
        }

        return AmneziaConfigStorage.TryReadStored(out var config, out _, out _)
            ? config
            : null;
    }

    public bool TryImportAmneziaConfigFile(string path, bool showErrors = true)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        if (!path.EndsWith(".conf", StringComparison.OrdinalIgnoreCase))
        {
            if (showErrors)
            {
                MessageBox.Show(
                    "Поддерживаются только файлы .conf",
                    "Ошибка",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }

            return false;
        }

        if (!AmneziaConfigStorage.TryImportFromFile(path, out var summary, out var error))
        {
            if (showErrors)
            {
                MessageBox.Show(error, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
            }

            return false;
        }

        ApplyProcessModeAmneziaSummary(summary);
        UpdateProcessModePreferences();
        RelayCommand.RaiseAllCanExecuteChanged();
        SetFooterLog($"Конфиг Amnezia загружен: {summary.Endpoint}");
        return true;
    }

    private void PickAmneziaConfigFile()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Выберите конфиг Amnezia",
            Filter = "Конфиг Amnezia (*.conf)|*.conf",
            DefaultExt = ".conf",
            CheckFileExists = true,
            Multiselect = false
        };

        if (dialog.ShowDialog() == true)
        {
            TryImportAmneziaConfigFile(dialog.FileName);
        }
    }

    private void ClearAmneziaConfig()
    {
        AmneziaConfigStorage.ClearStoredConfig();
        RefreshProcessModeAmneziaConfigState(string.Empty);
        UpdateProcessModePreferences();
        RelayCommand.RaiseAllCanExecuteChanged();
        SetFooterLog("Конфиг Amnezia удалён");
    }

    private void RefreshProcessModeAmneziaConfigState(string? sourceFileName)
    {
        if (!AmneziaConfigStorage.HasStoredConfig
            || !AmneziaConfigStorage.TryReadStored(out _, out var summary, out _))
        {
            HasProcessModeAmneziaConfig = false;
            ProcessModeAmneziaEndpoint = string.Empty;
            ProcessModeAmneziaSourceName = string.Empty;
            return;
        }

        HasProcessModeAmneziaConfig = true;
        ProcessModeAmneziaEndpoint = summary.Endpoint;
        ProcessModeAmneziaSourceName = !string.IsNullOrWhiteSpace(sourceFileName)
            ? sourceFileName
            : summary.SourceFileName;
    }

    private void ApplyProcessModeAmneziaSummary(AmneziaConfigSummary summary)
    {
        HasProcessModeAmneziaConfig = true;
        ProcessModeAmneziaEndpoint = summary.Endpoint;
        ProcessModeAmneziaSourceName = summary.SourceFileName;
        _settings.ProcessModeAmneziaSourceName = summary.SourceFileName;
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
        _settings.RunAsAdministrator = RunAsAdministrator;
        _settings.StartProxyWithApp = StartProxyWithApp;
        _settings.StartProcessModeWithApp = AppBranding.IsProcessModeUiVisible && StartProcessModeWithApp;
        _settings.StartBypassWithApp = RunAsAdministrator && StartBypassWithApp;
        _settings.StartMinimizedToTray = StartMinimizedToTray;
        _settings.NotifyOnMinimizeToTray = NotifyOnMinimizeToTray;
        _settings.UpdateListsOnStartup = UpdateListsOnStartup;
    }

    private void UpdateFooter()
    {
        FooterRight = WindowsProxySettings.IsPacEnabled(out _) ? "PAC: вкл" : "PAC: выкл";
    }

    private void SetFooterLog(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        AppLog.Info(message);
        FooterLog = message;
    }

    private async Task ToggleBypassAsync()
    {
        if (IsBypassRunning)
        {
            await StopBypassAsync();
            return;
        }

        await StartBypassAsync();
    }

    private async Task StartBypassAsync(bool silent = false)
    {
        if (IsBypassRunning)
        {
            if (BypassEnableTelegram && !_bypassService.IsTelegramRunning)
            {
                if (!InputParser.TryParsePort(TgWsProxyPort, out var resumeTgPort, out var resumePortError))
                {
                    if (!silent)
                    {
                        MessageBox.Show(resumePortError, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }

                    return;
                }

                _settings.TgWsProxyPort = resumeTgPort;
                IsBusy = true;
                SetFooterLog("Запуск Telegram-прокси...");
                try
                {
                    var progress = new Progress<BypassProgressReport>(HandleBypassProgress);
                    await _bypassService.StartTelegramAsync(
                        resumeTgPort,
                        _settings.TgWsProxySecret,
                        progress,
                        CancellationToken.None);
                    ApplyTelegramStartResult();
                    SaveBypassSettings(isActive: true);
                    UpdateBypassUi();
                    SetFooterLog("Telegram-прокси запущен");
                }
                catch (Exception ex)
                {
                    AppLog.Error(ex, "Ошибка запуска Telegram-прокси");
                    SetFooterLog($"Ошибка запуска Telegram-прокси: {ex.Message}");
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

            return;
        }

        if (!BypassEnableZapret && !BypassEnableTelegram)
        {
            if (!silent)
            {
                MessageBox.Show(
                    "Выберите хотя бы один сервис: YouTube/Discord или Telegram.",
                    "Ошибка",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }

            return;
        }

        if (!InputParser.TryParsePort(TgWsProxyPort, out var tgPort, out var portError))
        {
            if (!silent)
            {
                MessageBox.Show(portError, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
            }

            return;
        }

        _settings.TgWsProxyPort = tgPort;

        if (BypassEnableZapret && !HasZapretStrategy)
        {
            _bypassProbeFromStart = true;
            SetBypassProbingState(true, 0, 0);
        }

        IsBusy = true;
        SetFooterLog("Запуск обхода...");

        try
        {
            var progress = new Progress<BypassProgressReport>(HandleBypassProgress);
            await Task.Run(async () =>
                await _bypassService.StartAsync(
                    BypassEnableZapret,
                    BypassEnableTelegram,
                    _settings.SavedZapretStrategy,
                    tgPort,
                    _settings.TgWsProxySecret,
                    progress,
                    CancellationToken.None).ConfigureAwait(false)).ConfigureAwait(true);

            if (BypassEnableZapret && !string.IsNullOrWhiteSpace(_bypassService.ActiveZapretStrategy))
            {
                _settings.SavedZapretStrategy = _bypassService.ActiveZapretStrategy;
            }

            if (BypassEnableTelegram && !string.IsNullOrWhiteSpace(_bypassService.TelegramSecret))
            {
                ApplyTelegramStartResult();
            }

            SaveBypassSettings(isActive: true);
            UpdateBypassUi();
            SetFooterLog("Обход запущен");
        }
        catch (Exception ex)
        {
            AppLog.Error(ex, "Ошибка запуска обхода");
            await _bypassService.StopAsync(BypassEnableZapret, BypassEnableTelegram);
            UpdateBypassUi();
            SetFooterLog($"Ошибка запуска обхода: {ex.Message}");
            if (!silent)
            {
                MessageBox.Show(ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        finally
        {
            _bypassProbeFromStart = false;
            SetBypassProbingState(false, 0, 0);
            IsBusy = false;
        }
    }

    private async Task ProbeBypassStrategyAsync()
    {
        var wasZapretRunning = _bypassService.IsZapretRunning;
        _bypassProbeFromStart = false;
        SetBypassProbingState(true, 0, 0);
        IsBusy = true;
        SetFooterLog("Подбор стратегии zapret...");

        try
        {
            var progress = new Progress<BypassProgressReport>(HandleBypassProgress);
            var preferred = _bypassService.ActiveZapretStrategy ?? _settings.SavedZapretStrategy;
            var strategy = await Task.Run(async () =>
                await _bypassService.ProbeStrategyAsync(preferred, progress, CancellationToken.None)
                    .ConfigureAwait(false)).ConfigureAwait(true);
            _settings.SavedZapretStrategy = strategy;

            if (!wasZapretRunning)
            {
                await _bypassService.StopAsync(stopZapret: true, stopTelegram: false);
            }

            SaveBypassSettings(isActive: IsBypassRunning);
            UpdateBypassUi();
            BypassStatus = $"Стратегия подобрана: {strategy}";
            SetFooterLog($"Стратегия zapret обновлена: {strategy}");
        }
        catch (Exception ex)
        {
            AppLog.Error(ex, "Ошибка подбора стратегии zapret");
            if (!wasZapretRunning && _bypassService.IsZapretRunning)
            {
                await _bypassService.StopAsync(stopZapret: true, stopTelegram: false);
            }

            UpdateBypassUi();
            var message = FormatBypassProbeError(ex);
            SetFooterLog($"Ошибка подбора стратегии: {message}");
            MessageBox.Show(message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            SetBypassProbingState(false, 0, 0);
            IsBusy = false;
        }
    }

    private static string FormatBypassProbeError(Exception ex)
    {
        for (var current = ex; current != null; current = current.InnerException)
        {
            if (current is System.Net.Http.HttpRequestException
                && current.Message.Contains("SSL connection", StringComparison.OrdinalIgnoreCase))
            {
                return "Не удалось установить SSL-соединение с YouTube или Discord. "
                    + "Проверка продолжится со следующей стратегией; если ошибка повторяется — отключите Secure DNS в браузере.";
            }

            if (current is System.Net.Sockets.SocketException { ErrorCode: 10061 })
            {
                return "Не удалось подключиться к локальному прокси (127.0.0.1:10808). "
                    + "Отключите PAC или запустите локальный прокси перед подбором стратегии.";
            }
        }

        return ex.InnerException?.Message ?? ex.Message;
    }

    private void HandleBypassProgress(BypassProgressReport report)
    {
        if (report.ProbeCurrent is > 0 && report.ProbeTotal is > 0)
        {
            SetBypassProbingState(true, report.ProbeCurrent.Value, report.ProbeTotal.Value);
            SetFooterLog($"Подбор стратегии {report.ProbeCurrent}/{report.ProbeTotal}...");
        }

        if (!string.IsNullOrWhiteSpace(report.StatusMessage))
        {
            BypassStatus = report.StatusMessage;
            SetFooterLog(report.StatusMessage);
        }
    }

    private void SetBypassProbingState(bool isProbing, int current, int total)
    {
        _isBypassProbingStrategy = isProbing;
        _bypassProbeCurrent = current;
        _bypassProbeTotal = total;
        OnPropertyChanged(nameof(IsBypassProbingStrategy));
        OnPropertyChanged(nameof(IsBypassEditingEnabled));
        OnPropertyChanged(nameof(IsTgWsProxyPortEditingEnabled));
        OnPropertyChanged(nameof(BypassToggleLabel));
        OnPropertyChanged(nameof(BypassProbeLabel));
        OnPropertyChanged(nameof(CanStartBypass));
        OnPropertyChanged(nameof(CanToggleBypass));
        OnPropertyChanged(nameof(CanProbeBypassStrategy));
        RelayCommand.RaiseAllCanExecuteChanged();
    }

    private async Task StopBypassAsync()
    {
        IsBusy = true;
        SetFooterLog("Остановка обхода...");

        try
        {
            await _bypassService.StopAsync(BypassEnableZapret, BypassEnableTelegram);
            _telegramLinkCopiedToClipboard = false;
            SaveBypassSettings(isActive: false);
            UpdateBypassUi();
            BypassStatus = "Обход: остановлен";
            SetFooterLog("Обход остановлен");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void RefreshAvailableZapretStrategies()
    {
        _isRefreshingZapretStrategies = true;
        try
        {
            AvailableZapretStrategies.Clear();
            foreach (var strategy in ZapretInstaller.DiscoverStrategies())
            {
                AvailableZapretStrategies.Add(strategy);
            }
        }
        finally
        {
            _isRefreshingZapretStrategies = false;
        }
    }

    private void SyncSelectedZapretStrategyFromSettings()
    {
        var strategy = _bypassService.ActiveZapretStrategy ?? _settings.SavedZapretStrategy;
        var normalized = string.IsNullOrWhiteSpace(strategy) ? null : strategy;

        if (string.Equals(_selectedZapretStrategy, normalized, StringComparison.OrdinalIgnoreCase))
        {
            BypassActiveStrategy = strategy ?? string.Empty;
            return;
        }

        _isRefreshingZapretStrategies = true;
        try
        {
            _selectedZapretStrategy = normalized;
            BypassActiveStrategy = strategy ?? string.Empty;
            OnPropertyChanged(nameof(SelectedZapretStrategy));
        }
        finally
        {
            _isRefreshingZapretStrategies = false;
        }
    }

    private void UpdateBypassUi()
    {
        RefreshAvailableZapretStrategies();
        SyncSelectedZapretStrategyFromSettings();
        TelegramProxyLink = _bypassService.IsTelegramRunning
            ? _bypassService.TelegramProxyLink
            : BuildTelegramProxyLinkPreview(_settings.TgWsProxyPort, _settings.TgWsProxySecret);
        BypassStatus = IsBypassRunning ? "Обход: активен" : "Обход: остановлен";

        if (!IsBypassRunning)
        {
            FooterBypassStatus = "Обход: остановлен";
        }
        else if (_bypassService.IsZapretRunning && _bypassService.IsTelegramRunning)
        {
            FooterBypassStatus = "Обход: zapret + TG";
        }
        else if (_bypassService.IsZapretRunning)
        {
            var strategy = BypassActiveStrategy;
            FooterBypassStatus = string.IsNullOrWhiteSpace(strategy)
                ? "Обход: zapret"
                : $"Обход: {strategy}";
        }
        else
        {
            FooterBypassStatus = "Обход: Telegram";
        }

        UpdateBypassInfoText();
        NotifyBypassCommandState();
        OnPropertyChanged(nameof(IsBypassRunning));
        OnPropertyChanged(nameof(IsBypassZapretRunning));
        OnPropertyChanged(nameof(IsBypassTelegramRunning));
        OnPropertyChanged(nameof(IsBypassEditingEnabled));
        OnPropertyChanged(nameof(IsTgWsProxyPortEditingEnabled));
        OnPropertyChanged(nameof(BypassToggleLabel));
    }

    private void UpdateBypassInfoText()
    {
        var parts = new List<string>();
        if (BypassEnableZapret)
        {
            parts.Add("Для YouTube и Discord WPT нужно запустить от имени администратора.");
        }

        if (IsBypassTelegramRunning && _telegramLinkCopiedToClipboard && !string.IsNullOrWhiteSpace(TelegramProxyLink))
        {
            parts.Add("Ссылка tg://proxy в буфере обмена — в Telegram Desktop: Настройки → Продвинутые → Тип соединения → добавить MTProto-прокси.");
        }

        BypassInfoText = string.Join(" ", parts);
    }

    private void NotifyBypassCommandState()
    {
        OnPropertyChanged(nameof(HasZapretStrategy));
        OnPropertyChanged(nameof(CanStartBypass));
        OnPropertyChanged(nameof(CanToggleBypass));
        OnPropertyChanged(nameof(CanProbeBypassStrategy));
        OnPropertyChanged(nameof(BypassProbeLabel));
        RelayCommand.RaiseAllCanExecuteChanged();
    }

    private void CopyTelegramProxyLinkToClipboard()
    {
        if (string.IsNullOrWhiteSpace(TelegramProxyLink))
        {
            return;
        }

        System.Windows.Clipboard.SetText(TelegramProxyLink);
        _telegramLinkCopiedToClipboard = true;
        UpdateBypassInfoText();
    }

    private void ApplyTelegramStartResult()
    {
        if (string.IsNullOrWhiteSpace(_bypassService.TelegramSecret))
        {
            return;
        }

        _settings.TgWsProxySecret = _bypassService.TelegramSecret;
        TelegramProxyLink = _bypassService.TelegramProxyLink;
        CopyTelegramProxyLinkToClipboard();
    }

    private void TryAdoptBypassState()
    {
        _bypassService.TryAdoptExisting(_settings.SavedZapretStrategy);
        UpdateBypassUi();
    }

    private async Task RestoreBypassAsync(bool silent = false)
    {
        await _bypassService.TryAdoptExistingAsync(_settings.SavedZapretStrategy);
        UpdateBypassUi();

        if (_bypassService.IsZapretRunning && BypassEnableZapret)
        {
            UpdateBypassUi();
            SetFooterLog("Zapret уже запущен — подключено к существующему процессу");
        }

        if (IsBypassRunning)
        {
            if (BypassEnableTelegram && !_bypassService.IsTelegramRunning)
            {
                if (!InputParser.TryParsePort(TgWsProxyPort, out var tgPort, out var portError))
                {
                    if (!silent)
                    {
                        MessageBox.Show(portError, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }

                    return;
                }

                _settings.TgWsProxyPort = tgPort;
                IsBusy = true;
                SetFooterLog("Запуск Telegram-прокси...");
                try
                {
                    var progress = new Progress<BypassProgressReport>(HandleBypassProgress);
                    await _bypassService.StartTelegramAsync(
                        tgPort,
                        _settings.TgWsProxySecret,
                        progress,
                        CancellationToken.None);
                    ApplyTelegramStartResult();
                    SetFooterLog("Telegram-прокси запущен");
                }
                catch (Exception ex)
                {
                    AppLog.Error(ex, "Ошибка запуска Telegram-прокси при восстановлении обхода");
                    SetFooterLog($"Ошибка запуска Telegram-прокси: {ex.Message}");
                    if (!silent)
                    {
                        MessageBox.Show(ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    }

                    return;
                }
                finally
                {
                    IsBusy = false;
                }
            }

            SaveBypassSettings(isActive: true);
            UpdateBypassUi();

            if (_bypassService.IsZapretRunning)
            {
                SetFooterLog("Обход восстановлен");
            }

            return;
        }

        SetFooterLog("Восстановление обхода...");
        await StartBypassAsync(silent);
    }

    private void SaveBypassSettings(bool isActive)
    {
        _settings.BypassEnableZapret = BypassEnableZapret;
        _settings.BypassEnableTelegram = BypassEnableTelegram;
        _settings.IsBypassActive = isActive;

        if (InputParser.TryParsePort(TgWsProxyPort, out var tgPort, out _))
        {
            _settings.TgWsProxyPort = tgPort;
        }

        if (!string.IsNullOrWhiteSpace(_selectedZapretStrategy))
        {
            _settings.SavedZapretStrategy = _selectedZapretStrategy;
        }
        else if (!string.IsNullOrWhiteSpace(_bypassService.ActiveZapretStrategy))
        {
            _settings.SavedZapretStrategy = _bypassService.ActiveZapretStrategy;
        }

        if (!string.IsNullOrWhiteSpace(_bypassService.TelegramSecret))
        {
            _settings.TgWsProxySecret = _bypassService.TelegramSecret;
        }

        SettingsService.Save(_settings);
        NotifyBypassCommandState();
    }

    private static string BuildTelegramProxyLinkPreview(int port, string secret)
    {
        if (string.IsNullOrWhiteSpace(secret))
        {
            return string.Empty;
        }

        return $"tg://proxy?server=127.0.0.1&port={port}&secret=dd{secret}";
    }
}
