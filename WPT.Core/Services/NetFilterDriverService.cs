using System.Diagnostics;
using System.Runtime.InteropServices;
using System.ServiceProcess;

namespace WPT.Core.Services;

public static class NetFilterDriverService
{
    private const string DriverServiceName = "netfilter2";

    private static readonly string SystemDriverPath =
        Path.Combine(Environment.SystemDirectory, "drivers", "netfilter2.sys");

    public static void EnsureInstalled(IProgress<string>? progress)
    {
        AdminHelper.EnsureAdminOrThrow();

        if (!File.Exists(AppPaths.NetFilterDriver))
        {
            throw new FileNotFoundException("Не найден nfdriver.sys.", AppPaths.NetFilterDriver);
        }

        var bundledVersion = GetFileVersion(AppPaths.NetFilterDriver);
        var systemVersion = File.Exists(SystemDriverPath) ? GetFileVersion(SystemDriverPath) : null;

        if (!File.Exists(SystemDriverPath))
        {
            Install(progress);
            return;
        }

        if (ShouldReinstall(bundledVersion, systemVersion))
        {
            progress?.Report("Обновление драйвера netfilter2...");
            Uninstall();
            Install(progress);
        }
    }

    private static bool ShouldReinstall(string? bundledVersion, string? systemVersion)
    {
        if (string.IsNullOrWhiteSpace(bundledVersion) || string.IsNullOrWhiteSpace(systemVersion))
        {
            return !string.Equals(bundledVersion, systemVersion, StringComparison.OrdinalIgnoreCase);
        }

        if (Version.TryParse(bundledVersion, out var bundled) && Version.TryParse(systemVersion, out var system))
        {
            if (bundled > system)
            {
                return true;
            }

            return bundled.Major != system.Major;
        }

        return !bundledVersion.Equals(systemVersion, StringComparison.OrdinalIgnoreCase);
    }

    private static void Install(IProgress<string>? progress)
    {
        progress?.Report("Установка драйвера netfilter2...");

        try
        {
            File.Copy(AppPaths.NetFilterDriver, SystemDriverPath, overwrite: true);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Не удалось скопировать netfilter2.sys: {ex.Message}", ex);
        }

        if (!RedirectorNative.RegisterDriver(DriverServiceName))
        {
            throw new InvalidOperationException("Не удалось зарегистрировать драйвер netfilter2.");
        }
    }

    public static void Uninstall()
    {
        try
        {
            using var service = new ServiceController(DriverServiceName);
            if (service.Status == ServiceControllerStatus.Running)
            {
                service.Stop();
                service.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(15));
            }
        }
        catch
        {
        }

        if (!File.Exists(SystemDriverPath))
        {
            return;
        }

        RedirectorNative.UnregisterDriver(DriverServiceName);

        try
        {
            File.Delete(SystemDriverPath);
        }
        catch
        {
        }
    }

    private static string? GetFileVersion(string path)
    {
        try
        {
            return FileVersionInfo.GetVersionInfo(path).FileVersion;
        }
        catch
        {
            return null;
        }
    }

}
