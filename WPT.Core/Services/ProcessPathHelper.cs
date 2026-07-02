using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace WPT.Core.Services;

internal static class ProcessPathHelper
{
    public static string? TryGetExecutablePath(Process process)
    {
        try
        {
            var capacity = 1024;
            var builder = new StringBuilder(capacity);

            while (true)
            {
                var size = capacity;
                if (QueryFullProcessImageName(process.Handle, 0, builder, ref size))
                {
                    return builder.ToString();
                }

                if (Marshal.GetLastWin32Error() != 122)
                {
                    return null;
                }

                capacity *= 2;
                if (capacity > 32_768)
                {
                    return null;
                }

                builder = new StringBuilder(capacity);
            }
        }
        catch
        {
            return null;
        }
    }

    public static bool IsExecutableFromDirectory(Process process, string directoryPath)
    {
        var path = TryGetExecutablePath(process);
        return path != null
            && path.StartsWith(directoryPath, StringComparison.OrdinalIgnoreCase);
    }

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool QueryFullProcessImageName(
        IntPtr processHandle,
        int flags,
        StringBuilder exeName,
        ref int size);
}
