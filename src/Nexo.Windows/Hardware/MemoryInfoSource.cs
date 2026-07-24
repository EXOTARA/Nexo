using System.Runtime.InteropServices;

namespace Nexo.Windows.Hardware;

public interface IMemoryInfoSource
{
    ulong? ReadTotalPhysicalBytes();
}

public sealed class WinApiMemoryInfoSource : IMemoryInfoSource
{
    public ulong? ReadTotalPhysicalBytes()
    {
        try
        {
            var status = new MemoryStatusEx { Length = (uint)Marshal.SizeOf<MemoryStatusEx>() };
            if (!GlobalMemoryStatusEx(ref status) || status.TotalPhysical == 0)
            {
                return null;
            }

            return status.TotalPhysical;
        }
        catch (Exception)
        {
            return null;
        }
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx buffer);

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
}
