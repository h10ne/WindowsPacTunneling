namespace WindowPacTunneling.Ui;

public static class TunnelingLayout
{
    private const int AddButtonWidth = 40;
    private const int InputRowHeight = 30;
    private const int SelectedListHeight = 96;

    public static void Configure(TabPage tab, Control proxyRow, Label lblAvailableLists, ComboBox cmbAvailableLists,
        Label lblSelectedLists, Panel pnlSelectedLists, Label lblCustomDomains, Panel pnlCustomDomains,
        TextBox txtAddDomain, Button btnAddDomain, Label lblCustomIps, Panel pnlCustomIps,
        TextBox txtAddIp, Button btnAddIp, Button btnApply, Button btnShowPac, Button btnDisable)
    {
        tab.Controls.Clear();

        var table = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 12,
            BackColor = UiTheme.TabActive,
            Padding = new Padding(0)
        };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, SelectedListHeight));
        table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        table.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
        table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        table.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
        table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 50F));

        proxyRow.Dock = DockStyle.Fill;
        ConfigureCombo(cmbAvailableLists);
        ConfigureFixedListPanel(pnlSelectedLists);
        ConfigureStretchListPanel(pnlCustomDomains);
        ConfigureStretchListPanel(pnlCustomIps);

        var addDomainRow = CreateAddRow(txtAddDomain, btnAddDomain);
        var addIpRow = CreateAddRow(txtAddIp, btnAddIp);

        table.Controls.Add(proxyRow, 0, 0);
        table.Controls.Add(lblAvailableLists, 0, 1);
        table.Controls.Add(cmbAvailableLists, 0, 2);
        table.Controls.Add(lblSelectedLists, 0, 3);
        table.Controls.Add(pnlSelectedLists, 0, 4);
        table.Controls.Add(lblCustomDomains, 0, 5);
        table.Controls.Add(pnlCustomDomains, 0, 6);
        table.Controls.Add(addDomainRow, 0, 7);
        table.Controls.Add(lblCustomIps, 0, 8);
        table.Controls.Add(pnlCustomIps, 0, 9);
        table.Controls.Add(addIpRow, 0, 10);
        table.Controls.Add(CreateActionButtonsRow(btnApply, btnShowPac, btnDisable), 0, 11);

        foreach (Control control in new Control[]
                 {
                     lblAvailableLists, lblSelectedLists, lblCustomDomains, lblCustomIps
                 })
        {
            control.Dock = DockStyle.Fill;
            control.Margin = new Padding(0, 6, 0, 4);
            control.AutoSize = true;
        }

        tab.Controls.Add(table);
    }

    private static void ConfigureFieldLabel(Label label, Padding margin)
    {
        label.AutoSize = true;
        label.Margin = margin;
        label.Dock = DockStyle.Fill;
        label.TextAlign = ContentAlignment.MiddleLeft;
        label.Padding = new Padding(0, 0, 4, 0);
        label.UseCompatibleTextRendering = true;
    }

    public static Panel CreateProxyRow(Label lblProxy, ComboBox cmbProxy, Label lblPacPort, ComboBox cmbPacPort)
    {
        var row = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 2,
            Margin = new Padding(0, 0, 0, 4)
        };
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        row.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        row.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        ConfigureFieldLabel(lblProxy, new Padding(0, 0, 12, 4));
        ConfigureFieldLabel(lblPacPort, new Padding(8, 0, 0, 4));
        cmbProxy.Dock = DockStyle.Fill;
        cmbProxy.Margin = new Padding(0, 0, 8, 0);
        cmbPacPort.Dock = DockStyle.Fill;
        cmbPacPort.Margin = new Padding(8, 0, 0, 0);
        ConfigureCombo(cmbProxy);
        ConfigureCombo(cmbPacPort);

        row.Controls.Add(lblProxy, 0, 0);
        row.Controls.Add(lblPacPort, 1, 0);
        row.Controls.Add(cmbProxy, 0, 1);
        row.Controls.Add(cmbPacPort, 1, 1);

        var wrapper = new Panel { Dock = DockStyle.Fill, Height = 58, BackColor = UiTheme.TabActive };
        wrapper.Controls.Add(row);
        row.Dock = DockStyle.Fill;
        return wrapper;
    }

    public static void ConfigureStatusBar(Panel footer, Label lblStatus)
    {
        footer.Dock = DockStyle.Bottom;
        footer.Height = 36;
        footer.Padding = new Padding(0, 8, 0, 6);
        footer.BackColor = UiTheme.FormBackground;

        lblStatus.Dock = DockStyle.Fill;
        lblStatus.AutoSize = false;
        lblStatus.TextAlign = ContentAlignment.MiddleLeft;

        footer.Controls.Add(lblStatus);
    }

    private static Panel CreateActionButtonsRow(Button btnApply, Button btnShowPac, Button btnDisable)
    {
        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            MinimumSize = new Size(0, 46),
            Margin = new Padding(0, 4, 0, 0),
            BackColor = UiTheme.TabActive
        };

        btnApply.Anchor = AnchorStyles.Top | AnchorStyles.Left;
        btnApply.Size = new Size(148, 38);

        btnShowPac.Anchor = AnchorStyles.Top;
        btnShowPac.Size = new Size(148, 38);

        btnDisable.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        btnDisable.Size = new Size(148, 38);

        panel.Controls.AddRange([btnApply, btnShowPac, btnDisable]);
        panel.Resize += (_, _) => LayoutActionButtons(panel, btnApply, btnShowPac, btnDisable);
        LayoutActionButtons(panel, btnApply, btnShowPac, btnDisable);

        return panel;
    }

    private static void LayoutActionButtons(Panel panel, Button btnApply, Button btnShowPac, Button btnDisable)
    {
        var client = panel.ClientSize;
        btnApply.Location = new Point(0, 0);
        btnDisable.Location = new Point(client.Width - btnDisable.Width, 0);
        btnShowPac.Location = new Point((client.Width - btnShowPac.Width) / 2, 0);
    }

    private static Panel CreateAddRow(TextBox textBox, Button addButton)
    {
        var row = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Height = InputRowHeight,
            Margin = new Padding(0, 4, 0, 0)
        };
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, AddButtonWidth));
        row.RowStyles.Add(new RowStyle(SizeType.Absolute, InputRowHeight));

        textBox.Dock = DockStyle.Fill;
        textBox.Margin = new Padding(0, 0, 8, 0);
        textBox.MinimumSize = new Size(0, InputRowHeight);
        textBox.Multiline = false;

        addButton.Dock = DockStyle.Fill;
        addButton.Margin = new Padding(0);
        addButton.MinimumSize = new Size(AddButtonWidth, InputRowHeight);
        addButton.MaximumSize = new Size(AddButtonWidth, InputRowHeight);

        row.Controls.Add(textBox, 0, 0);
        row.Controls.Add(addButton, 1, 0);

        var wrapper = new Panel { Dock = DockStyle.Fill, Height = InputRowHeight, BackColor = UiTheme.TabActive };
        wrapper.Controls.Add(row);
        row.Dock = DockStyle.Fill;
        return wrapper;
    }

    private static void ConfigureCombo(ComboBox comboBox)
    {
        comboBox.Dock = DockStyle.Fill;
        comboBox.Margin = new Padding(0, 0, 0, 4);
        comboBox.MinimumSize = new Size(0, 30);
    }

    private static void ConfigureFixedListPanel(Panel panel)
    {
        panel.Dock = DockStyle.Fill;
        panel.Margin = new Padding(0, 0, 0, 4);
        panel.MinimumSize = new Size(0, 48);
    }

    private static void ConfigureStretchListPanel(Panel panel)
    {
        panel.Dock = DockStyle.Fill;
        panel.Margin = new Padding(0, 0, 0, 4);
        panel.MinimumSize = new Size(0, 48);
    }
}
