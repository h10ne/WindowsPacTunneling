using System.Runtime.InteropServices;
using System.Windows.Threading;
using Clipboard = System.Windows.Forms.Clipboard;

namespace WPT.Wpf.Services;

internal static class ClipboardHelper
{
    private const int ClipboardOpenError = unchecked((int)0x800401D0);

    public static void SetText(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher == null)
        {
            throw new InvalidOperationException("Буфер обмена недоступен: UI-поток не инициализирован.");
        }

        if (!dispatcher.CheckAccess())
        {
            dispatcher.Invoke(() => SetTextCore(text), DispatcherPriority.Send);
            return;
        }

        SetTextCore(text);
    }

    private static void SetTextCore(string text)
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            try
            {
                Clipboard.SetText(text, System.Windows.Forms.TextDataFormat.UnicodeText);
                return;
            }
            catch (ExternalException ex) when (ex.HResult == ClipboardOpenError && attempt < 4)
            {
                Thread.Sleep(50);
            }
        }
    }

}
