using System.Diagnostics;
using System.Security.Principal;

namespace WPT.Core.Services;

public static class AdminHelper
{
    public static bool IsRunningAsAdmin()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }

    public static void EnsureAdminOrThrow()
    {
        if (!IsRunningAsAdmin())
        {
            throw new InvalidOperationException(
                "Process Mode требует запуска приложения от имени администратора (драйвер netfilter2).");
        }
    }

    public static void EnsureZapretAdminOrThrow()
    {
        if (!IsRunningAsAdmin())
        {
            throw new InvalidOperationException(
                "Обход YouTube/Discord требует запуска WPT от имени администратора.");
        }
    }

    public static bool TryRestartAsAdmin(string? arguments = null)
    {
        if (IsRunningAsAdmin())
        {
            return true;
        }

        var exePath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(exePath))
        {
            return true;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = exePath,
                Arguments = arguments ?? string.Empty,
                UseShellExecute = true,
                Verb = "runas"
            });
            return false;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return true;
        }
    }

}
