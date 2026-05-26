using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace WPT.Core.Services;

public static class RedirectorNative
{
    public enum DialName
    {
        AIO_FILTERLOOPBACK,
        AIO_FILTERINTRANET,
        AIO_FILTERPARENT,
        AIO_FILTERICMP,
        AIO_FILTERTCP,
        AIO_FILTERUDP,
        AIO_FILTERDNS,
        AIO_ICMPING,
        AIO_DNSONLY,
        AIO_DNSPROX,
        AIO_DNSHOST,
        AIO_DNSPORT,
        AIO_TGTHOST,
        AIO_TGTPORT,
        AIO_TGTUSER,
        AIO_TGTPASS,
        AIO_CLRNAME,
        AIO_ADDNAME,
        AIO_BYPNAME
    }

    private const string RedirectorLibrary = "Redirector.bin";

    private static bool _dllDirectoryConfigured;

    public static bool RegisterDriver(string name) => aio_register(name);

    public static bool UnregisterDriver(string name) => aio_unregister(name);

    public static void ConfigureSearchPath()
    {
        if (_dllDirectoryConfigured)
        {
            return;
        }

        AppPaths.EnsureRoot();
        if (!File.Exists(AppPaths.RedirectorBinary))
        {
            throw new FileNotFoundException("Не найден Redirector.bin.", AppPaths.RedirectorBinary);
        }

        if (!SetDllDirectory(AppPaths.BinDirectory))
        {
            throw new InvalidOperationException("Не удалось настроить каталог загрузки Redirector.");
        }

        _dllDirectoryConfigured = true;
    }

    public static void Start(
        string socksHost,
        int socksPort,
        IEnumerable<string> processPatterns,
        IProgress<string>? progress)
    {
        ConfigureSearchPath();
        NetFilterDriverService.EnsureInstalled(progress);

        Dial(DialName.AIO_FILTERLOOPBACK, false);
        Dial(DialName.AIO_FILTERINTRANET, true);
        Dial(DialName.AIO_FILTERPARENT, true);
        Dial(DialName.AIO_FILTERICMP, false);
        Dial(DialName.AIO_FILTERTCP, true);
        Dial(DialName.AIO_FILTERUDP, true);
        Dial(DialName.AIO_FILTERDNS, true);
        Dial(DialName.AIO_DNSONLY, false);
        Dial(DialName.AIO_DNSPROX, true);
        Dial(DialName.AIO_DNSHOST, "8.8.8.8");
        Dial(DialName.AIO_DNSPORT, "53");
        Dial(DialName.AIO_TGTHOST, socksHost);
        Dial(DialName.AIO_TGTPORT, socksPort.ToString());
        Dial(DialName.AIO_TGTUSER, string.Empty);
        Dial(DialName.AIO_TGTPASS, string.Empty);

        Dial(DialName.AIO_CLRNAME, string.Empty);
        foreach (var pattern in processPatterns
                     .Select(ToRedirectorPattern)
                     .Where(x => !string.IsNullOrWhiteSpace(x))
                     .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!DialValue(DialName.AIO_ADDNAME, pattern))
            {
                throw new InvalidOperationException($"Некорректное правило процесса: {pattern}");
            }
        }

        AddSelfBypassRules();

        progress?.Report("Запуск Redirector...");
        if (!aio_init())
        {
            throw new InvalidOperationException("Redirector не удалось запустить.");
        }
    }

    public static void Stop()
    {
        if (!_dllDirectoryConfigured)
        {
            return;
        }

        aio_free();
    }

    public static string ToRedirectorPattern(string application)
    {
        var value = application.Trim();
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        if (value.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            value = value[..^4];
        }

        value = value.Trim();
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        // Redirector использует std::wregex (ECMAScript) — без (?i) и сложных классов.
        // Как в Netch: подстрока имени процесса в полном пути, например "Discord".
        var escaped = Regex.Escape(value);
        var lower = value.ToLowerInvariant();
        if (string.Equals(value, lower, StringComparison.Ordinal))
        {
            return escaped;
        }

        return $"{escaped}|{Regex.Escape(lower)}";
    }

    private static void AddSelfBypassRules()
    {
        AddBypassPath(AppContext.BaseDirectory);
        AddBypassPath(AppPaths.Root);
        AddBypassPath(AppPaths.BinDirectory);

        var currentProcessPath = Environment.ProcessPath;
        if (!string.IsNullOrWhiteSpace(currentProcessPath))
        {
            AddBypassPath(currentProcessPath);
        }

        var singBox = Path.Combine(AppPaths.BinDirectory, "sing-box.exe");
        if (File.Exists(singBox))
        {
            AddBypassPath(singBox);
        }
    }

    private static void AddBypassPath(string path)
    {
        var normalized = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var pattern = $"^{Regex.Escape(normalized)}";
        Dial(DialName.AIO_BYPNAME, pattern);
    }

    private static void Dial(DialName name, bool value) => aio_dial(name, value.ToString().ToLowerInvariant());

    private static void Dial(DialName name, string value) => aio_dial(name, value);

    private static bool DialValue(DialName name, string value) => aio_dial(name, value);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool SetDllDirectory(string lpPathName);

    [DllImport(RedirectorLibrary, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
    private static extern bool aio_register(string value);

    [DllImport(RedirectorLibrary, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
    private static extern bool aio_unregister(string value);

    [DllImport(RedirectorLibrary, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
    private static extern bool aio_dial(DialName name, string value);

    [DllImport(RedirectorLibrary, CallingConvention = CallingConvention.Cdecl)]
    private static extern bool aio_init();

    [DllImport(RedirectorLibrary, CallingConvention = CallingConvention.Cdecl)]
    private static extern bool aio_free();

}
