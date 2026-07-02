using Serilog;
using Serilog.Events;

namespace WPT.Core.Services;

public static class AppLog
{
    private static bool _initialized;

    public static void Initialize()
    {
        if (_initialized)
        {
            return;
        }

        AppPaths.EnsureRoot();

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.File(
                path: Path.Combine(AppPaths.LogsDirectory, "wpt-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 30,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}",
                encoding: new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: true))
            .CreateLogger();

        _initialized = true;
        Info($"Запуск {AppBranding.DisplayName} {AppVersion.CurrentLabel}");
    }

    public static void Close()
    {
        if (!_initialized)
        {
            return;
        }

        Info("Завершение работы приложения");
        Log.CloseAndFlush();
        _initialized = false;
    }

    public static void Debug(string message) => Write(LogEventLevel.Debug, message);

    public static void Debug(Exception exception, string message) =>
        Write(LogEventLevel.Debug, message, exception);

    public static void Info(string message) => Write(LogEventLevel.Information, message);

    public static void Warning(string message) => Write(LogEventLevel.Warning, message);

    public static void Warning(Exception exception, string message) =>
        Write(LogEventLevel.Warning, message, exception);

    public static void Error(string message) => Write(LogEventLevel.Error, message);

    public static void Error(Exception exception, string message) =>
        Write(LogEventLevel.Error, message, exception);

    private static void Write(LogEventLevel level, string message, Exception? exception = null)
    {
        if (!_initialized)
        {
            Initialize();
        }

        Log.Write(level, exception, message);
    }
}
