namespace WPT.Core.Services;

public static class ProcessModePacConflict
{
    public static bool IsPacEnabled => WindowsProxySettings.IsPacEnabled(out _);

    public static bool IsLocalProxyListening(string proxyAddress)
    {
        if (!InputParser.TryParseProxyAddress(proxyAddress, out _, out var port, out _))
        {
            return false;
        }

        return LocalProxyService.IsPortListening(port);
    }

    public static bool ShouldWarn => IsPacEnabled;

    public static string BuildWarningMessage(string proxyAddress, int processModePort, bool isLocalProxyRunning)
    {
        var proxyState = isLocalProxyRunning && IsLocalProxyListening(proxyAddress)
            ? "запущен, но Discord всё равно идёт через PAC, а не Redirector"
            : "не запущен — Discord зависнет на «Проверка обновлений»";

        return
            "В Windows включён PAC. Discord и другие Chromium/Electron-приложения " +
            "используют системный прокси (" + proxyAddress + "), а не Redirector Process Mode (порт " +
            processModePort + ").\n\n" +
            "Такой трафик идёт на 127.0.0.1 — Redirector его не перехватывает, в логах не будет Discord.\n\n" +
            "Локальный прокси PAC (" + proxyAddress + "): " + proxyState + ".\n\n" +
            "Рекомендация: отключите PAC на вкладке «Тунелирование» и перезапустите Discord.";
    }

}
