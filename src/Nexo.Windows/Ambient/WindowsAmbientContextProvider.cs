using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Nexo.Core.Ambient;
using Nexo.Core.Vision;

namespace Nexo.Windows.Ambient;

public sealed class WindowsAmbientContextProvider : IAmbientContextProvider
{
    public AmbientContextSnapshot? Capture(long windowHandle)
    {
        var handle = new IntPtr(windowHandle);
        if (handle == IntPtr.Zero || !IsWindow(handle))
        {
            return null;
        }

        var title = ReadWindowTitle(handle);
        GetWindowThreadProcessId(handle, out var processId);
        var processName = ReadProcessName(processId);
        var isSensitive = VisionPrivacyPolicy.IsSensitive(title, processName);

        return new AmbientContextSnapshot(
            isSensitive ? null : title,
            isSensitive ? null : processName,
            isSensitive);
    }

    private static string ReadWindowTitle(IntPtr handle)
    {
        var length = GetWindowTextLength(handle);
        if (length <= 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder(length + 1);
        _ = GetWindowText(handle, builder, builder.Capacity);
        return builder.ToString().Trim();
    }

    private static string ReadProcessName(uint processId)
    {
        try
        {
            using var process = Process.GetProcessById((int)processId);
            return process.ProcessName;
        }
        catch
        {
            return string.Empty;
        }
    }

    [DllImport("user32.dll")]
    private static extern bool IsWindow(IntPtr handle);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr handle, StringBuilder text, int maxCount);

    [DllImport("user32.dll")]
    private static extern int GetWindowTextLength(IntPtr handle);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr handle, out uint processId);
}
