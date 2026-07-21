using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Nexo.Core.Metrics;
using Nexo.Core.Resources;

namespace Nexo.Windows.Resources;

public sealed class WindowsResourceGovernorService
{
    private const uint MonitorDefaultToNearest = 2;

    public ResourceGovernorDecision Evaluate(
        SystemSnapshot snapshot,
        long preferredExternalWindowHandle = 0,
        long excludedWindowHandle = 0)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var foreground = ReadForegroundState(
            new IntPtr(preferredExternalWindowHandle),
            new IntPtr(excludedWindowHandle));
        var power = ReadPowerState();

        return ResourceGovernorPolicy.Evaluate(new ResourceGovernorInput(
            snapshot,
            foreground.IsFullScreen,
            foreground.ProcessName,
            foreground.WindowTitle,
            power.IsOnBattery));
    }

    private static ForegroundState ReadForegroundState(
        IntPtr preferredExternalWindow,
        IntPtr excludedWindow)
    {
        var foreground = GetForegroundWindow();
        if (foreground == IntPtr.Zero || foreground == excludedWindow || IsNexoWindow(foreground))
        {
            foreground = preferredExternalWindow;
        }

        if (foreground == IntPtr.Zero || !IsWindow(foreground) || IsIconic(foreground))
        {
            return ForegroundState.Empty;
        }

        GetWindowThreadProcessId(foreground, out var processId);
        var processName = ReadProcessName(processId);
        var title = ReadWindowTitle(foreground);
        var isFullScreen = CoversMonitor(foreground);

        return new ForegroundState(processName, title, isFullScreen);
    }

    private static bool CoversMonitor(IntPtr windowHandle)
    {
        if (!GetWindowRect(windowHandle, out var windowRect))
        {
            return false;
        }

        var monitor = MonitorFromWindow(windowHandle, MonitorDefaultToNearest);
        if (monitor == IntPtr.Zero)
        {
            return false;
        }

        var monitorInfo = new MonitorInfo
        {
            Size = Marshal.SizeOf<MonitorInfo>()
        };

        if (!GetMonitorInfo(monitor, ref monitorInfo))
        {
            return false;
        }

        const int tolerance = 3;
        return Math.Abs(windowRect.Left - monitorInfo.Monitor.Left) <= tolerance &&
               Math.Abs(windowRect.Top - monitorInfo.Monitor.Top) <= tolerance &&
               Math.Abs(windowRect.Right - monitorInfo.Monitor.Right) <= tolerance &&
               Math.Abs(windowRect.Bottom - monitorInfo.Monitor.Bottom) <= tolerance;
    }

    private static bool IsNexoWindow(IntPtr windowHandle)
    {
        GetWindowThreadProcessId(windowHandle, out var processId);
        return processId == Environment.ProcessId;
    }

    private static string ReadProcessName(uint processId)
    {
        if (processId == 0)
        {
            return string.Empty;
        }

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

    private static string ReadWindowTitle(IntPtr windowHandle)
    {
        var length = GetWindowTextLength(windowHandle);
        if (length <= 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder(length + 1);
        _ = GetWindowText(windowHandle, builder, builder.Capacity);
        return builder.ToString().Trim();
    }

    private static PowerState ReadPowerState()
    {
        if (!GetSystemPowerStatus(out var status))
        {
            return PowerState.Unknown;
        }

        return new PowerState(status.AcLineStatus == 0);
    }

    private readonly record struct ForegroundState(
        string ProcessName,
        string WindowTitle,
        bool IsFullScreen)
    {
        public static ForegroundState Empty { get; } = new(string.Empty, string.Empty, false);
    }

    private readonly record struct PowerState(bool IsOnBattery)
    {
        public static PowerState Unknown { get; } = new(false);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MonitorInfo
    {
        public int Size;
        public NativeRect Monitor;
        public NativeRect WorkArea;
        public uint Flags;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SystemPowerStatus
    {
        public byte AcLineStatus;
        public byte BatteryFlag;
        public byte BatteryLifePercent;
        public byte SystemStatusFlag;
        public uint BatteryLifeTime;
        public uint BatteryFullLifeTime;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindow(IntPtr windowHandle);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsIconic(IntPtr windowHandle);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(IntPtr windowHandle, out NativeRect rect);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr windowHandle, uint flags);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr windowHandle, out uint processId);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowTextLength(IntPtr windowHandle);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr windowHandle, StringBuilder text, int maxCount);

    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetSystemPowerStatus(out SystemPowerStatus status);
}
