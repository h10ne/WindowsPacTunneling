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
    private readonly AppSettings _settings = SettingsService.Load();
    private readonly HashSet<string> _selectedListIds = new(StringComparer.OrdinalIgnoreCase);
    private readonly DispatcherTimer _dailyUpdateTimer;

    private string _proxyAddress = string.Empty;
    private string _pacPort = string.Empty;
    private string _proxyLink = string.Empty;
    private string _localPort = string.Empty;
    private string _newDomain = string.Empty;
    private string _newIp = string.Empty;
    private string _statusMessage = "Готово";
    private string _footerRight = "PAC: выкл";
    private string _footerProxyStatus = "Прокси: остановлен";
    private string _proxyState = "Прокси остановлен";
    private int _selectedSection;
    private bool _isBusy;
    private bool _startWithWindows;
    private bool _startProxyWithApp;
    private bool _startMinimizedToTray;
    private bool _notifyOnMinimizeToTray;
    private bool _updateListsOnStartup;
    private ServiceListDefinition? _selectedListToAdd;

    public MainViewModel()
    {
        SelectedLists = [];
        CustomDomains = [];
        CustomIps = [];
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

        LoadFromSettings();
    }

    public ObservableCollection<ListChipItem> SelectedLists { get; }

    public ObservableCollection<string> CustomDomains { get; }

    public ObservableCollection<string> CustomIps { get; }

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

    public bool IsSettingsPage
    {
        get => SelectedSection == 2;
        set { if (value) SelectedSection = 2; }
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

    public bool IsProxyEditingEnabled => !IsProxyRunning && !IsBusy;

    public string ProxyToggleLabel => IsProxyRunning ? "Остановить" : "Запустить";

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
                }
                else
                {
                    await StartLocalProxyAsync(silent: true);
                }
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
        SaveProxySettings(_localProxyService.IsRunning);
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
            UpdateProxyUi();
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
            SaveUiState();
        }
        catch
        {
        }

        _domainListService.Dispose();
        _pacHttpServer.Dispose();
        _localProxyService.Dispose();
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
        StartMinimizedToTray = _settings.StartMinimizedToTray;
        NotifyOnMinimizeToTray = _settings.NotifyOnMinimizeToTray;
        UpdateListsOnStartup = _settings.UpdateListsOnStartup;
        ProxyLink = _settings.ProxyLink;
        LocalPort = _settings.LocalProxyPort.ToString();
        if (InputParser.TryParsePort(LocalPort, out var localPort, out _))
        {
            _localProxyService.Prepare(localPort);
        }

        UpdateProxyUi();
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
        if (SelectedListToAdd == null)
        {
            return;
        }

        if (!_selectedListIds.Add(SelectedListToAdd.Id))
        {
            SelectedListToAdd = null;
            return;
        }

        SelectedLists.Add(new ListChipItem(SelectedListToAdd.Id, SelectedListToAdd.DisplayName));
        SelectedListToAdd = null;
        OnPropertyChanged(nameof(SelectedListToAdd));
    }

    private void RemoveSelectedList(ListChipItem item)
    {
        _selectedListIds.Remove(item.Id);
        SelectedLists.Remove(item);
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

            if (!silent)
            {
                StatusMessage = $"Локальный прокси: {_localProxyService.LocalProxyAddress}";
            }
        }
        catch (Exception ex)
        {
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
        UpdateProxyUi();
        UpdateFooter();
    }

    private void UpdateProxyUi()
    {
        var isRunning = _localProxyService.IsRunning;
        FooterProxyStatus = isRunning
            ? $"Прокси: работает · {_localProxyService.LocalProxyAddress}"
            : "Прокси: остановлен";
        ProxyState = isRunning
            ? $"Работает · {_localProxyService.LocalProxyAddress}"
            : "Остановлен";
        OnPropertyChanged(nameof(IsProxyRunning));
        OnPropertyChanged(nameof(IsProxyEditingEnabled));
        OnPropertyChanged(nameof(ProxyToggleLabel));
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
            StatusMessage = $"PAC активен: {pac.Value.DomainsCount} доменов, {pac.Value.SubnetsCount} подсетей";

            if (!silent)
            {
                MessageBox.Show(
                    $"PAC-файл применён.\n\nАдрес: {pacUrl}\nДоменов: {pac.Value.DomainsCount}\nПодсетей: {pac.Value.SubnetsCount}",
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
    }

    private void UpdateAppSettingsPreferences()
    {
        _settings.StartWithWindows = StartWithWindows;
        _settings.StartProxyWithApp = StartProxyWithApp;
        _settings.StartMinimizedToTray = StartMinimizedToTray;
        _settings.NotifyOnMinimizeToTray = NotifyOnMinimizeToTray;
        _settings.UpdateListsOnStartup = UpdateListsOnStartup;
    }

    private void UpdateFooter()
    {
        var systemEnabled = WindowsProxySettings.IsPacEnabled(out var pacUrl);
        if (systemEnabled)
        {
            var portLabel = TryExtractPortFromPacUrl(pacUrl) ?? PacPort;
            FooterRight = $"PAC: вкл · порт {portLabel}";
        }
        else
        {
            FooterRight = "PAC: выкл";
        }
    }

    private static string? TryExtractPortFromPacUrl(string? pacUrl)
    {
        if (string.IsNullOrWhiteSpace(pacUrl))
        {
            return null;
        }

        if (!Uri.TryCreate(pacUrl, UriKind.Absolute, out var uri))
        {
            return null;
        }

        return uri.Port.ToString();
    }
}
