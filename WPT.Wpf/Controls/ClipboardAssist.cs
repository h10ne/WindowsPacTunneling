using System.Windows;
using System.Windows.Input;
using WPT.Wpf.Services;
using WpfTextBox = System.Windows.Controls.TextBox;

namespace WPT.Wpf.Controls;

public static class ClipboardAssist
{
    static ClipboardAssist()
    {
        EventManager.RegisterClassHandler(
            typeof(WpfTextBox),
            CommandManager.PreviewExecutedEvent,
            new ExecutedRoutedEventHandler(OnPreviewClipboardCommand),
            true);
    }

    public static void EnsureInitialized()
    {
    }

    private static void OnPreviewClipboardCommand(object sender, ExecutedRoutedEventArgs e)
    {
        if (sender is not WpfTextBox textBox)
        {
            return;
        }

        if (e.Command == ApplicationCommands.Copy)
        {
            TryHandleCopy(textBox, e);
            return;
        }

        if (e.Command == ApplicationCommands.Cut)
        {
            TryHandleCut(textBox, e);
        }
    }

    private static void TryHandleCopy(WpfTextBox textBox, ExecutedRoutedEventArgs e)
    {
        var text = textBox.SelectedText;
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        e.Handled = true;
        ClipboardHelper.SetText(text);
    }

    private static void TryHandleCut(WpfTextBox textBox, ExecutedRoutedEventArgs e)
    {
        if (textBox.IsReadOnly)
        {
            return;
        }

        var text = textBox.SelectedText;
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        e.Handled = true;
        ClipboardHelper.SetText(text);
        textBox.SelectedText = string.Empty;
    }

}
