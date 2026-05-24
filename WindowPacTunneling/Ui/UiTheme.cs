namespace WindowPacTunneling.Ui;

public static class UiTheme
{
    public static readonly Color FormBackground = Color.FromArgb(28, 30, 34);
    public static readonly Color Surface = Color.FromArgb(36, 39, 44);
    public static readonly Color SurfaceElevated = Color.FromArgb(48, 51, 56);
    public static readonly Color Border = Color.FromArgb(64, 68, 76);
    public static readonly Color TextPrimary = Color.FromArgb(232, 234, 237);
    public static readonly Color TextSecondary = Color.FromArgb(160, 166, 176);
    public static readonly Color Accent = Color.FromArgb(45, 125, 154);
    public static readonly Color AccentHover = Color.FromArgb(58, 145, 176);
    public static readonly Color AccentPressed = Color.FromArgb(36, 105, 130);
    public static readonly Color Danger = Color.FromArgb(168, 62, 62);
    public static readonly Color DangerHover = Color.FromArgb(190, 78, 78);
    public static readonly Color TabInactive = Color.FromArgb(40, 43, 48);
    public static readonly Color TabActive = Color.FromArgb(48, 51, 56);

    public static Font UiFont { get; } = new("Segoe UI", 9.75F, FontStyle.Regular, GraphicsUnit.Point);
    public static Font TabFont { get; } = new("Segoe UI Semibold", 9.75F, FontStyle.Regular, GraphicsUnit.Point);
    public static Font ButtonFont { get; } = new("Segoe UI Semibold", 9.5F, FontStyle.Regular, GraphicsUnit.Point);
}
