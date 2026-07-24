using Microsoft.Win32;
using Nexo.Core.Hardware;

namespace Nexo.Windows.Hardware;

public interface IGraphicsInfoSource
{
    IReadOnlyList<GraphicsCapability> ReadAdapters();
}

public sealed class RegistryGraphicsInfoSource : IGraphicsInfoSource
{
    private static readonly string[] IntegratedAdapterMarkers =
    {
        "microsoft basic render",
        "microsoft basic display",
        "intel(r) hd graphics",
        "intel(r) uhd graphics",
        "intel(r) iris",
        "intel(r) arc(tm) integrated"
    };

    public IReadOnlyList<GraphicsCapability> ReadAdapters()
    {
        var adapters = new List<GraphicsCapability>();
        try
        {
            using var videoKey = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Video");
            if (videoKey is null)
            {
                return adapters;
            }

            foreach (var adapterGuid in videoKey.GetSubKeyNames())
            {
                var adapter = TryReadAdapter(videoKey, adapterGuid);
                if (adapter is not null)
                {
                    adapters.Add(adapter);
                }
            }
        }
        catch (Exception)
        {
            // Degradación segura: no se pudo enumerar ninguna GPU desde el registro.
        }

        return adapters;
    }

    private static GraphicsCapability? TryReadAdapter(RegistryKey videoKey, string adapterGuid)
    {
        try
        {
            using var deviceKey = videoKey.OpenSubKey($@"{adapterGuid}\0000");
            if (deviceKey is null)
            {
                return null;
            }

            var name = ReadAdapterName(deviceKey);
            if (string.IsNullOrWhiteSpace(name))
            {
                return null;
            }

            var dedicatedBytes = ReadAdapterMemory(deviceKey, "HardwareInformation.qwMemorySize");
            if (dedicatedBytes is null && deviceKey.GetValue("HardwareInformation.MemorySize") is int legacyBytes and > 0)
            {
                dedicatedBytes = legacyBytes;
            }

            var sharedBytes = ReadAdapterMemory(deviceKey, "HardwareInformation.SharedMemorySize");
            var isDedicated = ClassifyAsDedicated(name, dedicatedBytes);

            return new GraphicsCapability(name.Trim(), isDedicated, dedicatedBytes, sharedBytes);
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static string? ReadAdapterName(RegistryKey deviceKey)
    {
        var raw = deviceKey.GetValue("HardwareInformation.AdapterString");
        if (raw is string text && !string.IsNullOrWhiteSpace(text))
        {
            return text;
        }

        if (raw is byte[] bytes && bytes.Length > 0)
        {
            var decoded = System.Text.Encoding.Unicode.GetString(bytes).TrimEnd('\0');
            if (!string.IsNullOrWhiteSpace(decoded))
            {
                return decoded;
            }
        }

        return deviceKey.GetValue("DriverDesc") as string;
    }

    private static long? ReadAdapterMemory(RegistryKey deviceKey, string valueName)
    {
        var raw = deviceKey.GetValue(valueName);
        return raw switch
        {
            long longValue when longValue > 0 => longValue,
            int intValue when intValue > 0 => intValue,
            _ => null
        };
    }

    private static bool? ClassifyAsDedicated(string name, long? dedicatedBytes)
    {
        var lowered = name.ToLowerInvariant();
        if (IntegratedAdapterMarkers.Any(lowered.Contains))
        {
            return false;
        }

        if (dedicatedBytes is { } bytes)
        {
            return bytes >= 512L * 1024 * 1024;
        }

        return null;
    }
}
