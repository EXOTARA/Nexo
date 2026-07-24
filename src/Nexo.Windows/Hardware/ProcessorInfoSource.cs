using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace Nexo.Windows.Hardware;

public interface IProcessorInfoSource
{
    string? ReadProcessorName();

    string? ReadProcessorVendor();

    int? ReadLogicalProcessorCount();

    int? ReadPhysicalCoreCount();
}

public sealed class RegistryProcessorInfoSource : IProcessorInfoSource
{
    public string? ReadProcessorName()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0");
            var name = key?.GetValue("ProcessorNameString") as string;
            return string.IsNullOrWhiteSpace(name) ? null : name.Trim();
        }
        catch (Exception)
        {
            return null;
        }
    }

    public string? ReadProcessorVendor()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0");
            var vendor = key?.GetValue("VendorIdentifier") as string;
            return string.IsNullOrWhiteSpace(vendor) ? null : vendor.Trim();
        }
        catch (Exception)
        {
            return null;
        }
    }

    public int? ReadLogicalProcessorCount()
    {
        try
        {
            return Environment.ProcessorCount > 0 ? Environment.ProcessorCount : null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    public int? ReadPhysicalCoreCount()
    {
        const int RelationProcessorCore = 0;
        const int ErrorInsufficientBuffer = 122;

        var buffer = IntPtr.Zero;
        try
        {
            uint returnLength = 0;
            GetLogicalProcessorInformationEx(RelationProcessorCore, IntPtr.Zero, ref returnLength);
            if (returnLength == 0 || Marshal.GetLastWin32Error() != ErrorInsufficientBuffer)
            {
                return null;
            }

            buffer = Marshal.AllocHGlobal((int)returnLength);
            if (!GetLogicalProcessorInformationEx(RelationProcessorCore, buffer, ref returnLength))
            {
                return null;
            }

            var coreCount = 0;
            var offset = 0;
            while (offset < returnLength)
            {
                var relationship = Marshal.ReadInt32(buffer, offset);
                var size = Marshal.ReadInt32(buffer, offset + 4);
                if (size <= 0)
                {
                    break;
                }

                if (relationship == RelationProcessorCore)
                {
                    coreCount++;
                }

                offset += size;
            }

            return coreCount > 0 ? coreCount : null;
        }
        catch (Exception)
        {
            return null;
        }
        finally
        {
            if (buffer != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(buffer);
            }
        }
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetLogicalProcessorInformationEx(int relationshipType, IntPtr buffer, ref uint returnedLength);
}
