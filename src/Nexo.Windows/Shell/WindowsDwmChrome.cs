using System.Runtime.InteropServices;
using Microsoft.Win32;
using Nexo.Core.AdaptiveEngine;
using Nexo.Core.Shell;

namespace Nexo.Windows.Shell;

/// <summary>
/// Diseño D62 — le pide a DWM el fondo, las esquinas y la sombra de una ventana.
///
/// Es la parte que toca Win32 de <see cref="WindowBackdropPolicy"/>: la decisión de qué pedir vive
/// en Nexo.Core y se puede probar; aquí solo quedan las tres llamadas a
/// <c>DwmSetWindowAttribute</c> y la lectura de los dos ajustes de Windows que mandan sobre ellas.
///
/// Ninguna de las llamadas es obligatoria. Un atributo que esta versión de Windows no conozca
/// devuelve un HRESULT de error y la ventana se queda como estaba, que es un resultado legítimo:
/// opaca y con las esquinas del sistema.
/// </summary>
public static class WindowsDwmChrome
{
    private const int DwmwaUseImmersiveDarkMode = 20;
    private const int DwmwaWindowCornerPreference = 33;
    private const int DwmwaSystemBackdropType = 38;

    private const int DwmwcpDefault = 0;
    private const int DwmwcpDoNotRound = 1;
    private const int DwmwcpRound = 2;
    private const int DwmwcpRoundSmall = 3;

    private const int DwmsbtNone = 1;
    private const int DwmsbtMainWindow = 2;      // Mica
    private const int DwmsbtTransientWindow = 3; // Acrílico

    private const uint SpiGetHighContrast = 0x0042;
    private const uint HcfHighContrastOn = 0x00000001;

    /// <summary>Lee del sistema lo que la política necesita saber.</summary>
    public static WindowBackdropProbe ReadProbe(HardwarePerformanceMode performanceMode) =>
        new(
            Environment.OSVersion.Version.Build,
            IsTransparencyEnabled(),
            IsHighContrastOn(),
            performanceMode);

    /// <summary>
    /// Aplica la decisión a la ventana. Devuelve cierto solo si el fondo pedido se aceptó: quien
    /// llama necesita saberlo para pintar su propio fondo si no hubo desenfoque.
    /// </summary>
    public static bool TryApply(IntPtr windowHandle, WindowBackdropDecision decision)
    {
        ArgumentNullException.ThrowIfNull(decision);

        if (windowHandle == IntPtr.Zero)
        {
            return false;
        }

        // El modo oscuro va primero: es lo que decide de qué color dibuja DWM el borde y la sombra,
        // y sin él una ventana oscura queda enmarcada en un filo blanco.
        TrySetAttribute(windowHandle, DwmwaUseImmersiveDarkMode, 1);

        if (decision.Corner != WindowCorner.System)
        {
            TrySetAttribute(windowHandle, DwmwaWindowCornerPreference, CornerValue(decision.Corner));
        }

        var backdrop = decision.Backdrop switch
        {
            WindowBackdrop.Acrylic => DwmsbtTransientWindow,
            WindowBackdrop.Mica => DwmsbtMainWindow,
            _ => DwmsbtNone
        };

        var applied = TrySetAttribute(windowHandle, DwmwaSystemBackdropType, backdrop);
        return applied && decision.Backdrop != WindowBackdrop.None;
    }

    private static int CornerValue(WindowCorner corner) => corner switch
    {
        WindowCorner.Round => DwmwcpRound,
        WindowCorner.RoundSmall => DwmwcpRoundSmall,
        WindowCorner.Square => DwmwcpDoNotRound,
        _ => DwmwcpDefault
    };

    private static bool TrySetAttribute(IntPtr windowHandle, int attribute, int value)
    {
        try
        {
            return DwmSetWindowAttribute(windowHandle, attribute, ref value, sizeof(int)) == 0;
        }
        catch (DllNotFoundException)
        {
            // dwmapi.dll existe desde Windows Vista; si faltara, no hay nada que componer.
            return false;
        }
        catch (EntryPointNotFoundException)
        {
            return false;
        }
    }

    private static bool IsTransparencyEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");

            // Sin la clave, Windows se comporta como si estuviera activado.
            return key?.GetValue("EnableTransparency") is not int enabled || enabled != 0;
        }
        catch (Exception exception) when (
            exception is System.Security.SecurityException or UnauthorizedAccessException or IOException)
        {
            return true;
        }
    }

    private static bool IsHighContrastOn()
    {
        var info = new HighContrast { CbSize = (uint)Marshal.SizeOf<HighContrast>() };

        try
        {
            if (!SystemParametersInfo(SpiGetHighContrast, info.CbSize, ref info, 0))
            {
                return false;
            }
        }
        catch (EntryPointNotFoundException)
        {
            return false;
        }

        return (info.DwFlags & HcfHighContrastOn) != 0;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct HighContrast
    {
        public uint CbSize;
        public uint DwFlags;
        public IntPtr LpszDefaultScheme;
    }

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SystemParametersInfo(uint action, uint param, ref HighContrast data, uint winIni);
}
