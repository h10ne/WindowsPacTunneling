namespace WindowPacTunneling.Ui;

public static class ProxyLayout
{
    public static void Configure(
        TabPage tab,
        Label lblProxyLink,
        TextBox txtProxyLink,
        Label lblLocalPort,
        TextBox txtLocalPort,
        Label lblProxyHint,
        Label lblProxyState,
        Button btnStartProxy,
        Button btnStopProxy)
    {
        tab.Controls.Clear();

        var table = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 9,
            BackColor = UiTheme.TabActive,
            Padding = new Padding(0)
        };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        table.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        table.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        ConfigureFieldLabel(lblProxyLink);
        ConfigureFieldLabel(lblLocalPort);
        ConfigureFieldLabel(lblProxyHint, secondary: true);
        ConfigureFieldLabel(lblProxyState);

        txtProxyLink.Dock = DockStyle.Fill;
        txtProxyLink.Margin = new Padding(0, 0, 0, 8);
        txtProxyLink.Multiline = true;
        txtProxyLink.ScrollBars = ScrollBars.Vertical;
        txtProxyLink.MinimumSize = new Size(0, 120);
        txtProxyLink.PlaceholderText = "vless://uuid@server:443?security=reality&type=tcp#remark";

        txtLocalPort.Dock = DockStyle.Fill;
        txtLocalPort.Margin = new Padding(0, 0, 0, 8);
        txtLocalPort.MinimumSize = new Size(0, 30);

        lblProxyHint.Dock = DockStyle.Fill;
        lblProxyHint.Margin = new Padding(0, 4, 0, 12);
        lblProxyHint.AutoSize = true;
        lblProxyHint.MaximumSize = new Size(0, 0);

        lblProxyState.Dock = DockStyle.Fill;
        lblProxyState.Margin = new Padding(0, 0, 0, 12);
        lblProxyState.AutoSize = true;

        var buttonsRow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            WrapContents = false,
            FlowDirection = FlowDirection.LeftToRight,
            Margin = new Padding(0),
            BackColor = UiTheme.TabActive
        };

        btnStartProxy.AutoSize = false;
        btnStartProxy.Size = new Size(148, 38);
        btnStartProxy.Margin = new Padding(0, 0, 12, 0);

        btnStopProxy.AutoSize = false;
        btnStopProxy.Size = new Size(148, 38);
        btnStopProxy.Margin = new Padding(0);

        buttonsRow.Controls.Add(btnStartProxy);
        buttonsRow.Controls.Add(btnStopProxy);

        table.Controls.Add(lblProxyLink, 0, 0);
        table.Controls.Add(txtProxyLink, 0, 1);
        table.Controls.Add(lblLocalPort, 0, 2);
        table.Controls.Add(txtLocalPort, 0, 3);
        table.Controls.Add(lblProxyHint, 0, 4);
        table.Controls.Add(lblProxyState, 0, 5);
        table.Controls.Add(buttonsRow, 0, 6);

        tab.Controls.Add(table);
    }

    private static void ConfigureFieldLabel(Label label, bool secondary = false)
    {
        label.Dock = DockStyle.Fill;
        label.Margin = new Padding(0, 6, 0, 4);
        label.AutoSize = true;
        label.TextAlign = ContentAlignment.MiddleLeft;
        label.UseCompatibleTextRendering = true;
        label.ForeColor = secondary ? UiTheme.TextSecondary : UiTheme.TextPrimary;
        label.BackColor = Color.Transparent;
    }
}
