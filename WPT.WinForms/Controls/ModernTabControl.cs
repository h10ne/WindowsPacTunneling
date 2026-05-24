using WPT.WinForms.Ui;

namespace WPT.WinForms.Controls;

public sealed class ModernTabControl : TabControl
{
    private const int WmEraseBkgnd = 0x0014;

    public ModernTabControl()
    {
        SetStyle(
            ControlStyles.OptimizedDoubleBuffer
            | ControlStyles.AllPaintingInWmPaint
            | ControlStyles.Opaque
            | ControlStyles.ResizeRedraw,
            true);
        Appearance = TabAppearance.FlatButtons;
        DrawMode = TabDrawMode.OwnerDrawFixed;
        SizeMode = TabSizeMode.FillToRight;
        ItemSize = new Size(0, 36);
        Padding = new Point(12, 8);
        Font = UiTheme.TabFont;
        BackColor = UiTheme.Surface;
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WmEraseBkgnd)
        {
            using var graphics = Graphics.FromHwnd(Handle);
            PaintBackground(graphics);
            m.Result = (IntPtr)1;
            return;
        }

        base.WndProc(ref m);
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        PaintBackground(e.Graphics);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        PaintBackground(e.Graphics);
        PaintContentArea(e.Graphics);

        for (var i = 0; i < TabCount; i++)
        {
            PaintTab(e.Graphics, GetTabRect(i), i);
        }
    }

    protected override void OnDrawItem(DrawItemEventArgs e)
    {
        if (e.Index >= 0 && e.Index < TabCount)
        {
            PaintTab(e.Graphics, e.Bounds, e.Index);
        }
    }

    protected override void OnSelectedIndexChanged(EventArgs e)
    {
        base.OnSelectedIndexChanged(e);
        Invalidate();
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        Invalidate();
    }

    private static void PaintBackground(Graphics graphics, Rectangle bounds)
    {
        using var brush = new SolidBrush(UiTheme.Surface);
        graphics.FillRectangle(brush, bounds);
    }

    private void PaintBackground(Graphics graphics)
    {
        PaintBackground(graphics, ClientRectangle);
    }

    private void PaintContentArea(Graphics graphics)
    {
        var display = DisplayRectangle;
        using var contentBrush = new SolidBrush(UiTheme.TabActive);
        graphics.FillRectangle(contentBrush, display);

        using var borderPen = new Pen(UiTheme.Border);
        graphics.DrawRectangle(borderPen, display.X, display.Y, display.Width - 1, display.Height - 1);
    }

    private void PaintTab(Graphics graphics, Rectangle bounds, int index)
    {
        if (index < 0 || index >= TabPages.Count)
        {
            return;
        }

        var page = TabPages[index];
        var selected = index == SelectedIndex;
        var backColor = selected ? UiTheme.TabActive : UiTheme.TabInactive;
        var textColor = selected ? UiTheme.TextPrimary : UiTheme.TextSecondary;

        using var backBrush = new SolidBrush(backColor);
        graphics.FillRectangle(backBrush, bounds);

        using (var borderPen = new Pen(UiTheme.Border))
        {
            graphics.DrawRectangle(borderPen, bounds.Left, bounds.Top, bounds.Width - 1, bounds.Height - 1);
        }

        if (selected)
        {
            using var accentPen = new Pen(UiTheme.Accent, 2);
            graphics.DrawLine(accentPen, bounds.Left + 2, bounds.Bottom - 1, bounds.Right - 3, bounds.Bottom - 1);
        }

        TextRenderer.DrawText(
            graphics,
            page.Text,
            Font,
            bounds,
            textColor,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
    }
}
