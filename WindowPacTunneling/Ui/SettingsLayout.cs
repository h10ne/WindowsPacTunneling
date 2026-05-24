namespace WindowPacTunneling.Ui;

public static class SettingsLayout
{
    private const int ActionButtonRowHeight = 50;

    public static void Configure(
        TabPage tab,
        CheckBox chkStartWithWindows,
        CheckBox chkStartProxyWithApp,
        CheckBox chkNotifyOnMinimizeToTray,
        CheckBox chkUpdateListsOnStartup,
        Button btnUpdateLists,
        Button btnSave,
        Button btnOpenDataFolder)
    {
        tab.Controls.Clear();

        var table = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 6,
            BackColor = UiTheme.TabActive,
            Padding = new Padding(0)
        };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, ActionButtonRowHeight));

        ConfigureCheckBox(chkStartWithWindows);
        ConfigureCheckBox(chkStartProxyWithApp);
        ConfigureCheckBox(chkNotifyOnMinimizeToTray);
        ConfigureCheckBox(chkUpdateListsOnStartup);

        btnUpdateLists.AutoSize = true;
        btnUpdateLists.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        btnUpdateLists.MinimumSize = new Size(0, 32);
        btnUpdateLists.Margin = new Padding(0, 0, 0, 8);

        ConfigureActionButton(btnSave);

        btnOpenDataFolder.AutoSize = true;
        btnOpenDataFolder.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        btnOpenDataFolder.MinimumSize = new Size(148, 38);
        btnOpenDataFolder.Margin = new Padding(0, 4, 0, 0);

        table.Controls.Add(chkStartWithWindows, 0, 0);
        table.Controls.Add(chkStartProxyWithApp, 0, 1);
        table.Controls.Add(chkNotifyOnMinimizeToTray, 0, 2);
        table.Controls.Add(chkUpdateListsOnStartup, 0, 3);
        table.Controls.Add(btnUpdateLists, 0, 4);
        table.Controls.Add(CreateBottomButtonsRow(btnSave, btnOpenDataFolder), 0, 5);

        tab.Controls.Add(table);
    }

    private static void ConfigureCheckBox(CheckBox checkBox)
    {
        checkBox.AutoSize = true;
        checkBox.Margin = new Padding(0, 0, 0, 8);
    }

    private static void ConfigureActionButton(Button button)
    {
        button.AutoSize = false;
        button.Size = new Size(148, 38);
        button.Margin = new Padding(0, 4, 0, 0);
    }

    private static Panel CreateBottomButtonsRow(Button btnSave, Button btnOpenDataFolder)
    {
        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            MinimumSize = new Size(0, 46),
            Margin = new Padding(0, 4, 0, 0),
            BackColor = UiTheme.TabActive
        };

        btnSave.Anchor = AnchorStyles.Top | AnchorStyles.Left;
        btnOpenDataFolder.Anchor = AnchorStyles.Top | AnchorStyles.Right;

        panel.Controls.AddRange([btnSave, btnOpenDataFolder]);
        panel.Resize += (_, _) => LayoutBottomButtons(panel, btnSave, btnOpenDataFolder);
        LayoutBottomButtons(panel, btnSave, btnOpenDataFolder);

        return panel;
    }

    private static void LayoutBottomButtons(Panel panel, Button btnSave, Button btnOpenDataFolder)
    {
        var client = panel.ClientSize;
        btnSave.Location = new Point(0, 0);
        btnOpenDataFolder.Location = new Point(client.Width - btnOpenDataFolder.Width, 0);
    }
}
