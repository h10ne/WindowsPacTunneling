namespace WindowPacTunneling.Ui;

public static class SettingsLayout
{
    private const int ActionButtonRowHeight = 50;

    public static void Configure(
        TabPage tab,
        CheckBox chkStartWithWindows,
        CheckBox chkStartProxyWithApp,
        CheckBox chkNotifyOnMinimizeToTray,
        Button btnOpenDataFolder,
        Button btnSave)
    {
        tab.Controls.Clear();

        var table = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 5,
            BackColor = UiTheme.TabActive,
            Padding = new Padding(0)
        };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, ActionButtonRowHeight));

        ConfigureCheckBox(chkStartWithWindows);
        ConfigureCheckBox(chkStartProxyWithApp);
        ConfigureCheckBox(chkNotifyOnMinimizeToTray);

        btnOpenDataFolder.AutoSize = true;
        btnOpenDataFolder.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        btnOpenDataFolder.MinimumSize = new Size(0, 32);
        btnOpenDataFolder.Margin = new Padding(0, 4, 0, 8);

        btnSave.AutoSize = false;
        btnSave.Size = new Size(148, 38);
        btnSave.Margin = new Padding(0, 4, 0, 0);
        btnSave.Anchor = AnchorStyles.Top | AnchorStyles.Left;

        table.Controls.Add(chkStartWithWindows, 0, 0);
        table.Controls.Add(chkStartProxyWithApp, 0, 1);
        table.Controls.Add(chkNotifyOnMinimizeToTray, 0, 2);
        table.Controls.Add(btnOpenDataFolder, 0, 3);
        table.Controls.Add(btnSave, 0, 4);

        tab.Controls.Add(table);
    }

    private static void ConfigureCheckBox(CheckBox checkBox)
    {
        checkBox.AutoSize = true;
        checkBox.Margin = new Padding(0, 0, 0, 8);
    }
}
