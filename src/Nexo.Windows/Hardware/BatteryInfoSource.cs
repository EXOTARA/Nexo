using System.Runtime.InteropServices;

namespace Nexo.Windows.Hardware;

public interface IBatteryInfoSource
{
    bool? ReadHasBattery();
}

public sealed class WinApiBatteryInfoSource : IBatteryInfoSource
{
    private const byte NoSystemBattery = 128;
    private const byte UnknownBatteryFlag = 255;

    public bool? ReadHasBattery()
    {
        try
        {
            if (!GetSystemPowerStatus(out var status))
            {
                return null;
            }

            if (status.BatteryFlag == UnknownBatteryFlag)
            {
                return null;
            }

            return (status.BatteryFlag & NoSystemBattery) == 0;
        }
        catch (Exception)
        {
            return null;
        }
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetSystemPowerStatus(out SystemPowerStatus status);

    [StructLayout(LayoutKind.Sequential)]
    private struct SystemPowerStatus
    {
        public byte ACLineStatus;
        public byte BatteryFlag;
        public byte BatteryLifePercent;
        public byte SystemStatusFlag;
        public int BatteryLifeTime;
        public int BatteryFullLifeTime;
    }
}
