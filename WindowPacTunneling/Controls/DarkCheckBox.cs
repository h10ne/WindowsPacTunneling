using System.Drawing.Drawing2D;
using WindowPacTunneling.Ui;

namespace WindowPacTunneling.Controls;

public sealed class DarkCheckBox : CheckBox
{
    private const int BoxSize = 16;
    private const int BoxTextGap = 8;

    public DarkCheckBox()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        AutoSize = true;
        Cursor = Cursors.Hand;
        ForeColor = UiTheme.TextPrimary;
        BackColor = UiTheme.TabActive;
        UseVisualStyleBackColor = false;
    }

    protected override void OnPaint(PaintEventArgs pevent)
    {
        var g = pevent.Graphics;
        g.Clear(BackColor);

        var boxRect = new Rectangle(0, (Height - BoxSize) / 2, BoxSize, BoxSize);

        using (var fillBrush = new SolidBrush(Checked ? UiTheme.Accent : UiTheme.SurfaceElevated))
        using (var borderPen = new Pen(UiTheme.Border))
        {
            g.FillRectangle(fillBrush, boxRect);
            g.DrawRectangle(borderPen, boxRect.X, boxRect.Y, boxRect.Width - 1, boxRect.Height - 1);
        }

        if (Checked)
        {
            DrawCheckMark(g, boxRect);
        }

        if (string.IsNullOrEmpty(Text))
        {
            return;
        }

        var textRect = new Rectangle(BoxSize + BoxTextGap, 0, Width - BoxSize - BoxTextGap, Height);
        TextRenderer.DrawText(
            g,
            Text,
            Font,
            textRect,
            ForeColor,
            TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.NoPrefix);
    }

    protected override void OnCheckedChanged(EventArgs e)
    {
        base.OnCheckedChanged(e);
        Invalidate();
    }

    protected override void OnTextChanged(EventArgs e)
    {
        base.OnTextChanged(e);
        Invalidate();
    }

    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        Invalidate();
    }

    public override Size GetPreferredSize(Size proposedSize)
    {
        var height = Math.Max(24, Font.Height + 6);

        if (string.IsNullOrEmpty(Text))
        {
            return new Size(BoxSize, height);
        }

        var textWidth = TextRenderer.MeasureText(
            Text,
            Font,
            new Size(int.MaxValue, height),
            TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix).Width;

        return new Size(BoxSize + BoxTextGap + textWidth + 2, height);
    }

    private static void DrawCheckMark(Graphics g, Rectangle boxRect)
    {
        using var pen = new Pen(UiTheme.TextPrimary, 2f)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round,
            LineJoin = LineJoin.Round
        };

        g.DrawLines(
            pen,
            [
                new Point(boxRect.Left + 3, boxRect.Top + 8),
                new Point(boxRect.Left + 7, boxRect.Top + 12),
                new Point(boxRect.Left + 13, boxRect.Top + 4)
            ]);
    }
}
