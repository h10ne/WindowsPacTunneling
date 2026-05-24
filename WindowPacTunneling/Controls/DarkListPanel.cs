using WindowPacTunneling.Ui;

namespace WindowPacTunneling.Controls;

public static class DarkListPanel
{
    private const int ButtonSize = 22;
    private const int GapAfterButton = 6;

    public static readonly Color PanelBackground = UiTheme.Surface;
    public static readonly Color RowBackground = UiTheme.SurfaceElevated;
    public static readonly Color RowHover = Color.FromArgb(62, 66, 72);
    public static readonly Color RowText = Color.FromArgb(230, 232, 235);
    public static readonly Color RemoveNormal = Color.FromArgb(72, 76, 84);
    public static readonly Color RemoveHover = Color.FromArgb(160, 58, 58);

    public static void ApplyPanelStyle(Panel panel)
    {
        panel.BackColor = PanelBackground;
        panel.BorderStyle = BorderStyle.FixedSingle;
        panel.AutoScroll = true;
    }

    public static void AddRow(Panel container, ref int y, string text, Action onRemove)
    {
        var textOffset = ButtonSize + GapAfterButton;
        var rowWidth = Math.Max(100, container.ClientSize.Width - 24);

        var row = new Panel
        {
            Height = ButtonSize,
            Width = rowWidth,
            Location = new Point(4, y),
            BackColor = RowBackground,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };

        var removeButton = new Button
        {
            Text = "×",
            Location = new Point(0, 0),
            Size = new Size(ButtonSize, ButtonSize),
            FlatStyle = FlatStyle.Flat,
            TabStop = false,
            ForeColor = RowText,
            BackColor = RemoveNormal,
            Font = new Font("Segoe UI", 8F, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        removeButton.FlatAppearance.BorderSize = 0;
        removeButton.Click += (_, _) => onRemove();

        var label = new Label
        {
            Text = text,
            Location = new Point(textOffset, 0),
            Size = new Size(Math.Max(50, rowWidth - textOffset), ButtonSize),
            ForeColor = RowText,
            BackColor = Color.Transparent,
            TextAlign = ContentAlignment.MiddleLeft,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };

        void SetRowColor(Color color)
        {
            row.BackColor = color;
            removeButton.BackColor = color == RowHover ? RemoveHover : RemoveNormal;
        }

        void BindHover(Control control)
        {
            control.MouseEnter += (_, _) => SetRowColor(RowHover);
            control.MouseLeave += (_, _) =>
            {
                if (!row.ClientRectangle.Contains(row.PointToClient(Cursor.Position)))
                {
                    SetRowColor(RowBackground);
                }
            };
        }

        row.Resize += (_, _) =>
        {
            label.Width = Math.Max(50, row.ClientSize.Width - textOffset);
        };

        BindHover(row);
        BindHover(label);
        BindHover(removeButton);

        row.Controls.Add(removeButton);
        row.Controls.Add(label);
        container.Controls.Add(row);
        y += 24;
    }
}
