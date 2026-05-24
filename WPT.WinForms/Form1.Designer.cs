using WPT.WinForms.Controls;

namespace WPT.WinForms;

partial class Form1
{
    private System.ComponentModel.IContainer components = null;

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            components?.Dispose();
            _notifyIcon?.Dispose();
            _dailyUpdateTimer?.Dispose();
            _domainListService?.Dispose();
            _pacHttpServer?.Dispose();
            _localProxyService?.Dispose();
        }

        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        pnlFooter = new Panel();
        tabMain = new ModernTabControl();
        tabTunneling = new TabPage();
        tabProxy = new TabPage();
        tabSettings = new TabPage();
        lblProxy = new Label();
        cmbProxy = new ComboBox();
        lblPacPort = new Label();
        cmbPacPort = new ComboBox();
        lblAvailableLists = new Label();
        cmbAvailableLists = new ComboBox();
        lblSelectedLists = new Label();
        pnlSelectedLists = new Panel();
        lblCustomDomains = new Label();
        pnlCustomDomains = new Panel();
        txtAddDomain = new TextBox();
        btnAddDomain = new Button();
        lblCustomIps = new Label();
        pnlCustomIps = new Panel();
        txtAddIp = new TextBox();
        btnAddIp = new Button();
        lblProxyLink = new Label();
        txtProxyLink = new TextBox();
        lblLocalPort = new Label();
        txtLocalPort = new TextBox();
        lblProxyHint = new Label();
        lblProxyState = new Label();
        btnStartProxy = new Button();
        btnStopProxy = new Button();
        chkStartWithWindows = new DarkCheckBox();
        chkStartProxyWithApp = new DarkCheckBox();
        chkNotifyOnMinimizeToTray = new DarkCheckBox();
        chkUpdateListsOnStartup = new DarkCheckBox();
        btnUpdateLists = new Button();
        btnOpenDataFolder = new Button();
        btnSave = new Button();
        btnApply = new Button();
        btnShowPac = new Button();
        btnDisable = new Button();
        lblStatus = new Label();
        pnlFooter.SuspendLayout();
        tabMain.SuspendLayout();
        tabSettings.SuspendLayout();
        SuspendLayout();

        pnlFooter.Name = "pnlFooter";

        tabMain.Controls.Add(tabTunneling);
        tabMain.Controls.Add(tabProxy);
        tabMain.Controls.Add(tabSettings);
        tabMain.Dock = DockStyle.Fill;
        tabMain.Name = "tabMain";
        tabMain.Padding = new Point(12, 8);
        tabMain.SelectedIndex = 0;

        tabTunneling.Name = "tabTunneling";
        tabTunneling.Text = "Тунелирование";
        tabTunneling.UseVisualStyleBackColor = false;

        tabProxy.Name = "tabProxy";
        tabProxy.Text = "Прокси";
        tabProxy.UseVisualStyleBackColor = false;

        tabSettings.Name = "tabSettings";
        tabSettings.Text = "Настройки";
        tabSettings.UseVisualStyleBackColor = false;

        lblProxy.Name = "lblProxy";
        lblProxy.Text = "Прокси-сервер";
        cmbProxy.Name = "cmbProxy";
        cmbProxy.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
        cmbProxy.AutoCompleteSource = AutoCompleteSource.ListItems;
        lblPacPort.Name = "lblPacPort";
        lblPacPort.Text = "Порт PAC";
        cmbPacPort.Name = "cmbPacPort";
        cmbPacPort.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
        cmbPacPort.AutoCompleteSource = AutoCompleteSource.ListItems;
        lblAvailableLists.Name = "lblAvailableLists";
        lblAvailableLists.Text = "Списки сервисов";
        cmbAvailableLists.Name = "cmbAvailableLists";
        cmbAvailableLists.DropDownStyle = ComboBoxStyle.DropDownList;
        lblSelectedLists.Name = "lblSelectedLists";
        lblSelectedLists.Text = "Выбранное";
        pnlSelectedLists.Name = "pnlSelectedLists";
        lblCustomDomains.Name = "lblCustomDomains";
        lblCustomDomains.Text = "Свои домены";
        pnlCustomDomains.Name = "pnlCustomDomains";
        txtAddDomain.Name = "txtAddDomain";
        txtAddDomain.PlaceholderText = "example.com";
        btnAddDomain.Name = "btnAddDomain";
        btnAddDomain.Text = "+";
        lblCustomIps.Name = "lblCustomIps";
        lblCustomIps.Text = "Свои IP/CIDR";
        pnlCustomIps.Name = "pnlCustomIps";
        txtAddIp.Name = "txtAddIp";
        txtAddIp.PlaceholderText = "1.2.3.4 или 10.0.0.0/8";
        btnAddIp.Name = "btnAddIp";
        btnAddIp.Text = "+";
        lblProxyLink.Name = "lblProxyLink";
        lblProxyLink.Text = "Ссылка на прокси";
        txtProxyLink.Name = "txtProxyLink";
        lblLocalPort.Name = "lblLocalPort";
        lblLocalPort.Text = "Локальный порт";
        txtLocalPort.Name = "txtLocalPort";
        txtLocalPort.Text = "10808";
        lblProxyHint.Name = "lblProxyHint";
        lblProxyHint.Text = "Локальный прокси не меняет системные настройки. Адрес 127.0.0.1:порт укажите на вкладке «Тунелирование» и настройте PAC.";
        lblProxyState.Name = "lblProxyState";
        lblProxyState.Text = "Прокси остановлен";
        btnStartProxy.Name = "btnStartProxy";
        btnStartProxy.Text = "Запустить";
        btnStopProxy.Name = "btnStopProxy";
        btnStopProxy.Text = "Остановить";
        chkStartWithWindows.Name = "chkStartWithWindows";
        chkStartWithWindows.Text = "Запускать вместе со стартом системы";
        chkStartProxyWithApp.Name = "chkStartProxyWithApp";
        chkStartProxyWithApp.Text = "Запускать прокси со стартом приложения";
        chkNotifyOnMinimizeToTray.Name = "chkNotifyOnMinimizeToTray";
        chkNotifyOnMinimizeToTray.Text = "Уведомлять при сворачивании приложения";
        chkUpdateListsOnStartup.Name = "chkUpdateListsOnStartup";
        chkUpdateListsOnStartup.Text = "Обновлять списки при запуске приложения";
        btnUpdateLists.Name = "btnUpdateLists";
        btnUpdateLists.Text = "Обновить списки";
        btnOpenDataFolder.Name = "btnOpenDataFolder";
        btnOpenDataFolder.Text = "Открыть папку данных";
        btnSave.Name = "btnSave";
        btnSave.Text = "Сохранить";
        btnApply.Name = "btnApply";
        btnApply.Text = "Применить";
        btnShowPac.Name = "btnShowPac";
        btnShowPac.Text = "Показать PAC";
        btnDisable.Name = "btnDisable";
        btnDisable.Text = "Отключить";
        lblStatus.Name = "lblStatus";
        lblStatus.Text = "Готово";

        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(640, 700);
        Controls.Add(tabMain);
        Controls.Add(pnlFooter);
        MinimumSize = new Size(560, 620);
        Name = "Form1";
        Padding = new Padding(12, 12, 12, 0);
        StartPosition = FormStartPosition.CenterScreen;
        Text = "Window PAC Tunneling";
        pnlFooter.ResumeLayout(false);
        tabMain.ResumeLayout(false);
        tabSettings.ResumeLayout(false);
        tabSettings.PerformLayout();
        ResumeLayout(false);
    }

    private Panel pnlFooter;
    private ModernTabControl tabMain;
    private TabPage tabTunneling;
    private TabPage tabProxy;
    private TabPage tabSettings;
    private Label lblProxy;
    private ComboBox cmbProxy;
    private Label lblPacPort;
    private ComboBox cmbPacPort;
    private Label lblAvailableLists;
    private ComboBox cmbAvailableLists;
    private Label lblSelectedLists;
    private Panel pnlSelectedLists;
    private Label lblCustomDomains;
    private Panel pnlCustomDomains;
    private TextBox txtAddDomain;
    private Button btnAddDomain;
    private Label lblCustomIps;
    private Panel pnlCustomIps;
    private TextBox txtAddIp;
    private Button btnAddIp;
    private Label lblProxyLink;
    private TextBox txtProxyLink;
    private Label lblLocalPort;
    private TextBox txtLocalPort;
    private Label lblProxyHint;
    private Label lblProxyState;
    private Button btnStartProxy;
    private Button btnStopProxy;
    private CheckBox chkStartWithWindows;
    private CheckBox chkStartProxyWithApp;
    private CheckBox chkNotifyOnMinimizeToTray;
    private CheckBox chkUpdateListsOnStartup;
    private Button btnUpdateLists;
    private Button btnOpenDataFolder;
    private Button btnSave;
    private Button btnApply;
    private Button btnShowPac;
    private Button btnDisable;
    private Label lblStatus;
}
