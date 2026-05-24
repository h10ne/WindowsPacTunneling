using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace WPT.Core.Services;

public static class WindowsProxySettings
{
    private const int InternetOptionSettingsChanged = 39;
    private const int InternetOptionRefresh = 37;

    public static void EnablePac(string pacUrl)
    {
        using var key = Registry.CurrentUser.OpenSubKey(
            @"Software\Microsoft\Windows\CurrentVersion\Internet Settings",
            writable: true)
            ?? throw new InvalidOperationException("Не удалось открыть ключ реестра Internet Settings");

        key.SetValue("AutoConfigURL", pacUrl);
        key.SetValue("ProxyEnable", 0, RegistryValueKind.DWord);

        NotifyChanges();
    }

    public static void DisablePac()
    {
        using var key = Registry.CurrentUser.OpenSubKey(
            @"Software\Microsoft\Windows\CurrentVersion\Internet Settings",
            writable: true)
            ?? throw new InvalidOperationException("Не удалось открыть ключ реестра Internet Settings");

        key.DeleteValue("AutoConfigURL", throwOnMissingValue: false);
        key.SetValue("ProxyEnable", 0, RegistryValueKind.DWord);

        NotifyChanges();
    }

    public static bool IsPacEnabled(out string? pacUrl)
    {
        using var key = Registry.CurrentUser.OpenSubKey(
            @"Software\Microsoft\Windows\CurrentVersion\Internet Settings");

        var raw = key?.GetValue("AutoConfigURL");
        pacUrl = raw switch
        {
            string value when !string.IsNullOrWhiteSpace(value) => value.Trim(),
            _ => null
        };

        return pacUrl != null;
    }

    private static void NotifyChanges()
    {
        InternetSetOption(IntPtr.Zero, InternetOptionSettingsChanged, IntPtr.Zero, 0);
        InternetSetOption(IntPtr.Zero, InternetOptionRefresh, IntPtr.Zero, 0);
    }

    [DllImport("wininet.dll", SetLastError = true)]
    private static extern bool InternetSetOption(
        IntPtr hInternet,
        int dwOption,
        IntPtr lpBuffer,
        int dwBufferLength);
}
