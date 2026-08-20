using Microsoft.Win32;
using Nexo.Core.Shell;

namespace Nexo.Windows.Shell;

/// <summary>
/// Lee el color de acento vigente de Windows —el mismo que Personalización → Colores ya calcula a
/// partir del fondo de escritorio cuando "Color de acento automático" está activo— para que Sakura
/// pueda ofrecerlo como acento propio en vez de uno de los cuatro fijos.
/// </summary>
public static class WindowsAccentColorReader
{
    public static string? TryRead()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\DWM");
            if (key?.GetValue("AccentColor") is int packed)
            {
                return SystemAccentColor.ToHex(unchecked((uint)packed));
            }
        }
        catch (Exception exception) when (
            exception is System.Security.SecurityException or UnauthorizedAccessException or IOException)
        {
            // Sin permiso de lectura del registro o clave ausente: se queda con el acento manual.
        }

        return null;
    }
}
