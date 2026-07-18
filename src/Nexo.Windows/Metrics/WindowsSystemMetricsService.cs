using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Nexo.Core.Metrics;

namespace Nexo.Windows.Metrics;

public sealed class WindowsSystemMetricsService
{
    private static readonly TimeSpan TopProcessRefreshInterval = TimeSpan.FromSeconds(6);

    private readonly object _cpuLock = new();
    private readonly int _currentProcessId = Environment.ProcessId;
    private readonly GpuPerformanceCounterReader _gpuReader = new();

    private CpuTimes? _lastCpuTimes;
    private DateTimeOffset _lastTopProcessRead = DateTimeOffset.MinValue;
    private ProcessUsage _cachedTopProcess = ProcessUsage.Empty;

    public WindowsSystemMetricsService()
    {
        if (GetSystemTimes(out var idleFileTime, out var kernelFileTime, out var userFileTime))
        {
            _lastCpuTimes = new CpuTimes(
                ToUInt64(idleFileTime),
                ToUInt64(kernelFileTime),
                ToUInt64(userFileTime));
        }
    }

    public SystemSnapshot ReadSnapshot()
    {
        var memory = ReadMemory();
        var gpu = _gpuReader.Read();
        var topProcess = ReadTopProcessWithCache();

        return new SystemSnapshot(
            CpuUsagePercent: ReadCpuUsage(),
            MemoryUsagePercent: memory.UsagePercent,
            UsedMemoryBytes: memory.UsedBytes,
            TotalMemoryBytes: memory.TotalBytes,
            GpuUsagePercent: gpu.UsagePercent,
            DedicatedGpuMemoryBytes: gpu.DedicatedMemoryBytes,
            SystemDriveUsagePercent: ReadSystemDriveUsage(),
            TopProcessName: topProcess.Name,
            TopProcessWorkingSetBytes: topProcess.WorkingSetBytes,
            CapturedAt: DateTimeOffset.Now);
    }

    private double? ReadCpuUsage()
    {
        if (!GetSystemTimes(out var idleFileTime, out var kernelFileTime, out var userFileTime))
        {
            return null;
        }

        var current = new CpuTimes(
            ToUInt64(idleFileTime),
            ToUInt64(kernelFileTime),
            ToUInt64(userFileTime));

        lock (_cpuLock)
        {
            if (_lastCpuTimes is null)
            {
                _lastCpuTimes = current;
                return null;
            }

            var previous = _lastCpuTimes.Value;
            _lastCpuTimes = current;

            if (current.Idle < previous.Idle
                || current.Kernel < previous.Kernel
                || current.User < previous.User)
            {
                return null;
            }

            var idleDelta = current.Idle - previous.Idle;
            var kernelDelta = current.Kernel - previous.Kernel;
            var userDelta = current.User - previous.User;
            var totalDelta = kernelDelta + userDelta;

            if (totalDelta == 0 || idleDelta > totalDelta)
            {
                return null;
            }

            var busyDelta = totalDelta - idleDelta;
            var usage = busyDelta * 100d / totalDelta;
            return Math.Clamp(usage, 0, 100);
        }
    }

    private static MemoryUsage ReadMemory()
    {
        var status = new MemoryStatusEx
        {
            Length = (uint)Marshal.SizeOf<MemoryStatusEx>()
        };

        if (!GlobalMemoryStatusEx(ref status) || status.TotalPhysical == 0)
        {
            return MemoryUsage.Empty;
        }

        var used = status.TotalPhysical - status.AvailablePhysical;
        var percentage = used * 100d / status.TotalPhysical;
        return new MemoryUsage(Math.Clamp(percentage, 0, 100), used, status.TotalPhysical);
    }

    private static double? ReadSystemDriveUsage()
    {
        try
        {
            var root = Path.GetPathRoot(Environment.SystemDirectory);
            if (string.IsNullOrWhiteSpace(root))
            {
                return null;
            }

            var drive = new DriveInfo(root);
            if (!drive.IsReady || drive.TotalSize <= 0)
            {
                return null;
            }

            var used = drive.TotalSize - drive.AvailableFreeSpace;
            return Math.Clamp(used * 100d / drive.TotalSize, 0, 100);
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    private ProcessUsage ReadTopProcessWithCache()
    {
        var now = DateTimeOffset.UtcNow;
        if (now - _lastTopProcessRead < TopProcessRefreshInterval)
        {
            return _cachedTopProcess;
        }

        _lastTopProcessRead = now;
        _cachedTopProcess = ReadTopProcess();
        return _cachedTopProcess;
    }

    private ProcessUsage ReadTopProcess()
    {
        var best = ProcessUsage.Empty;

        foreach (var process in Process.GetProcesses())
        {
            using (process)
            {
                try
                {
                    if (process.Id == _currentProcessId || process.HasExited)
                    {
                        continue;
                    }

                    var workingSet = process.WorkingSet64;
                    if (workingSet <= (best.WorkingSetBytes ?? -1))
                    {
                        continue;
                    }

                    best = new ProcessUsage(process.ProcessName, workingSet);
                }
                catch (InvalidOperationException)
                {
                    // El proceso terminó mientras se estaba leyendo.
                }
                catch (System.ComponentModel.Win32Exception)
                {
                    // Algunos procesos protegidos no permiten consultar todos sus datos.
                }
                catch (NotSupportedException)
                {
                    // Un proceso remoto o especial puede no exponer esta información.
                }
            }
        }

        return best;
    }

    private static ulong ToUInt64(FileTime fileTime)
    {
        return ((ulong)fileTime.HighDateTime << 32) | fileTime.LowDateTime;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetSystemTimes(
        out FileTime idleTime,
        out FileTime kernelTime,
        out FileTime userTime);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx buffer);

    [StructLayout(LayoutKind.Sequential)]
    private struct FileTime
    {
        public uint LowDateTime;
        public uint HighDateTime;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MemoryStatusEx
    {
        public uint Length;
        public uint MemoryLoad;
        public ulong TotalPhysical;
        public ulong AvailablePhysical;
        public ulong TotalPageFile;
        public ulong AvailablePageFile;
        public ulong TotalVirtual;
        public ulong AvailableVirtual;
        public ulong AvailableExtendedVirtual;
    }

    private readonly record struct CpuTimes(ulong Idle, ulong Kernel, ulong User);

    private readonly record struct MemoryUsage(double UsagePercent, ulong UsedBytes, ulong TotalBytes)
    {
        public static MemoryUsage Empty { get; } = new(0, 0, 0);
    }

    private readonly record struct ProcessUsage(string? Name, long? WorkingSetBytes)
    {
        public static ProcessUsage Empty { get; } = new(null, null);
    }
}
