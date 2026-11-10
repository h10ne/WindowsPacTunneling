using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace WPT.TraySupport;

/// <summary> Оформление меню трея в стиле WPT: тема Windows, скруглённые углы, точки состояния </summary>
public static class TrayMenuStyle
{
    private static bool _isDark = IsDarkTheme();

    /// <summary> Применить стиль к уже собранному меню (вместе с подменю) </summary>
    /// <param name="menu"> <see cref="ContextMenuStrip"/> </param>
    public static void Apply(ContextMenuStrip menu)
    {
        menu.Font = new Font("Segoe UI", 10f);
        menu.ShowImageMargin = true;
        // Тема могла смениться, пока меню было закрыто
        menu.Opening += (_, _) => _isDark = IsDarkTheme();
        StyleDropDown(menu, new WptMenuRenderer());

        if (menu.Items.Count > 0)
        {
            menu.Items[0].Font = new Font(menu.Font, FontStyle.Bold);
        }
    }

    /// <summary> Точка состояния слева от пункта, как в сайдбаре WPT </summary>
    /// <param name="item"> <see cref="ToolStripMenuItem"/> </param>
    /// <param name="isActive"> Запущено / включено </param>
    public static void SetStatus(ToolStripMenuItem item, bool isActive)
    {
        var dot = isActive ? (_isDark ? ActiveDotDark : ActiveDotLight) : InactiveDot;
        if (!ReferenceEquals(item.Image, dot))
        {
            item.Image = dot;
        }
    }

    private static readonly Bitmap InactiveDot = CreateDot(Color.FromArgb(0x6B, 0x6B, 0x6E));
    private static readonly Bitmap ActiveDotDark = CreateDot(Color.FromArgb(0x6C, 0xCB, 0x5F));
    private static readonly Bitmap ActiveDotLight = CreateDot(Color.FromArgb(0x0F, 0x7B, 0x0F));

    private static void StyleDropDown(ToolStripDropDown dropDown, WptMenuRenderer renderer)
    {
        dropDown.Renderer = renderer;
        dropDown.Padding = new Padding(0, 4, 0, 4);
        dropDown.MinimumSize = new Size(190, 0);
        dropDown.Opened += (_, _) => ApplyWindowFrame(dropDown.Handle);

        foreach (var item in dropDown.Items.OfType<ToolStripMenuItem>())
        {
            item.Padding = new Padding(4, 5, 4, 5);
            if (item.HasDropDownItems && item.DropDown is ToolStripDropDownMenu child)
            {
                child.ShowImageMargin = false;
                StyleDropDown(child, renderer);
            }
        }
    }

    // Windows 11: скругление, тень и цвет рамки силами DWM. На Windows 10 атрибуты молча не применяются
    private static void ApplyWindowFrame(IntPtr handle)
    {
        var corner = DwmCornerRound;
        DwmSetWindowAttribute(handle, DwmWindowCornerPreference, ref corner, sizeof(int));
        var border = ColorTranslator.ToWin32(Palette.Current.Border);
        DwmSetWindowAttribute(handle, DwmBorderColor, ref border, sizeof(int));
    }

    private static Bitmap CreateDot(Color color)
    {
        var bitmap = new Bitmap(16, 16);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var brush = new SolidBrush(color);
        graphics.FillEllipse(brush, 4, 4, 8, 8);
        return bitmap;
    }

    private static bool IsDarkTheme()
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
        return key?.GetValue("AppsUseLightTheme") is not int light || light == 0;
    }

    private static GraphicsPath CreateRoundedRect(Rectangle rect, int radius)
    {
        var diameter = radius * 2;
        var path = new GraphicsPath();
        path.AddArc(rect.X, rect.Y, diameter, diameter, 180, 90);
        path.AddArc(rect.Right - diameter, rect.Y, diameter, diameter, 270, 90);
        path.AddArc(rect.Right - diameter, rect.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(rect.X, rect.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }

    /// <summary> Цвета меню в тёмной и светлой теме (как MenuFlyout в WinUI) </summary>
    /// <param name="Background"> <see cref="Color"/> </param>
    /// <param name="Border"> <see cref="Color"/> </param>
    /// <param name="Hover"> <see cref="Color"/> </param>
    /// <param name="Text"> <see cref="Color"/> </param>
    /// <param name="DisabledText"> <see cref="Color"/> </param>
    private sealed record Palette(Color Background, Color Border, Color Hover, Color Text, Color DisabledText)
    {
        private static readonly Palette Dark = new(
            Color.FromArgb(0x2C, 0x2C, 0x2C),
            Color.FromArgb(0x3A, 0x3A, 0x3E),
            Color.FromArgb(0x3D, 0x3D, 0x3D),
            Color.FromArgb(0xF0, 0xF0, 0xF0),
            Color.FromArgb(0x78, 0x78, 0x78));

        private static readonly Palette Light = new(
            Color.FromArgb(0xF9, 0xF9, 0xF9),
            Color.FromArgb(0xE0, 0xE0, 0xE0),
            Color.FromArgb(0xEA, 0xEA, 0xEA),
            Color.FromArgb(0x1B, 0x1B, 0x1B),
            Color.FromArgb(0xA0, 0xA0, 0xA0));

        public static Palette Current => _isDark ? Dark : Light;
    }

    private sealed class WptMenuRenderer : ToolStripRenderer
    {
        private static readonly bool HasDwmFrame = Environment.OSVersion.Version.Build >= 22000;

        protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e) =>
            e.Graphics.Clear(Palette.Current.Background);

        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
        {
            // На Windows 11 рамку рисует DWM вместе со скруглением
            if (HasDwmFrame)
            {
                return;
            }

            using var pen = new Pen(Palette.Current.Border);
            e.Graphics.DrawRectangle(pen, 0, 0, e.AffectedBounds.Width - 1, e.AffectedBounds.Height - 1);
        }

        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            if (!e.Item.Selected || !e.Item.Enabled)
            {
                return;
            }

            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var path = CreateRoundedRect(new Rectangle(4, 1, e.Item.Width - 9, e.Item.Height - 3), 4);
            using var brush = new SolidBrush(Palette.Current.Hover);
            e.Graphics.FillPath(brush, path);
        }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e) =>
            TextRenderer.DrawText(
                e.Graphics,
                e.Text,
                e.TextFont,
                e.TextRectangle,
                e.Item.Enabled ? Palette.Current.Text : Palette.Current.DisabledText,
                e.TextFormat);

        protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
        {
            e.ArrowColor = e.Item?.Enabled == false ? Palette.Current.DisabledText : Palette.Current.Text;
            base.OnRenderArrow(e);
        }

        protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
        {
            var y = e.Item.Height / 2;
            using var pen = new Pen(Palette.Current.Border);
            e.Graphics.DrawLine(pen, 12, y, e.Item.Width - 12, y);
        }
    }

    private const int DwmWindowCornerPreference = 33;
    private const int DwmBorderColor = 34;
    private const int DwmCornerRound = 2;

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
}
