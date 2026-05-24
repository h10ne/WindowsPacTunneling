using WindowPacTunneling.Controls;

namespace WindowPacTunneling;

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
        }

        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        pnlFooter = new Panel();
        tabMain = new ModernTabControl();
        tabTunneling = new TabPage();
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
        chkStartWithWindows = new CheckBox();
        btnOpenDataFolder = new Button();
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
        tabMain.Controls.Add(tabSettings);
        tabMain.Dock = DockStyle.Fill;
        tabMain.Name = "tabMain";
        tabMain.Padding = new Point(16, 6);
        tabMain.SelectedIndex = 0;
        tabMain.SizeMode = TabSizeMode.Fixed;

        tabTunneling.Name = "tabTunneling";
        tabTunneling.Text = "Тунелирование";
        tabTunneling.UseVisualStyleBackColor = false;

        tabSettings.Controls.Add(chkStartWithWindows);
        tabSettings.Controls.Add(btnOpenDataFolder);
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
        chkStartWithWindows.Name = "chkStartWithWindows";
        chkStartWithWindows.Text = "Запускать вместе со стартом системы";
        chkStartWithWindows.Location = new Point(12, 16);
        chkStartWithWindows.AutoSize = true;
        btnOpenDataFolder.Name = "btnOpenDataFolder";
        btnOpenDataFolder.Text = "Открыть папку данных";
        btnOpenDataFolder.Location = new Point(12, 52);
        btnOpenDataFolder.AutoSize = true;
        btnOpenDataFolder.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        btnOpenDataFolder.MinimumSize = new Size(0, 32);
        btnOpenDataFolder.Anchor = AnchorStyles.Top | AnchorStyles.Left;
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
    private CheckBox chkStartWithWindows;
    private Button btnOpenDataFolder;
    private Button btnApply;
    private Button btnShowPac;
    private Button btnDisable;
    private Label lblStatus;
}
