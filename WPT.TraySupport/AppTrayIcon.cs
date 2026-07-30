using System.Drawing;

namespace WPT.TraySupport;

public sealed class AppTrayIcon : IDisposable
{
    private readonly NotifyIcon _notifyIcon;

    public AppTrayIcon(Icon? icon, string tooltip)
    {
        _notifyIcon = new NotifyIcon
        {
            Icon = icon ?? SystemIcons.Shield,
            Text = tooltip,
            Visible = false
        };
    }

    public event EventHandler? DoubleClick;

    public ContextMenuStrip ContextMenu => _notifyIcon.ContextMenuStrip ??= new ContextMenuStrip();

    public bool Visible
    {
        get => _notifyIcon.Visible;
        set => _notifyIcon.Visible = value;
    }

    public void ShowBalloon(string title, string message, ToolTipIcon icon = ToolTipIcon.Info, int timeoutMs = 2000) =>
        _notifyIcon.ShowBalloonTip(timeoutMs, title, message, icon);

    public void Dispose()
    {
        _notifyIcon.Dispose();
    }

    public void WireDoubleClick()
    {
        _notifyIcon.DoubleClick += (_, _) => DoubleClick?.Invoke(this, EventArgs.Empty);
    }

}
