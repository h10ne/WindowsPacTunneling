namespace WPT.TraySupport;

public interface ITrayWindow
{
    void Show();

    void Hide();

    void Activate();

    void AttachClosing(Func<bool> cancelCloseHandler);

    void RunOnUiThread(Action action);
}
