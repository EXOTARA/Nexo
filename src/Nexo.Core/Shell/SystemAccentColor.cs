namespace Nexo.Core.Shell;

/// <summary>
/// Diseño D28 — "que los colores de la app se adapten al tema del usuario". Windows guarda su color
/// de acento vigente —el mismo que ya usa para bordes de ventana y mosaicos, y que en Windows 11 se
/// deriva del fondo de escritorio cuando "Color del acento automático" está activo— en el registro,
/// como un DWORD empaquetado 0xAABBGGRR. Aquí solo vive la conversión pura a "#RRGGBB", separada de
/// quien lee el registro, para poder probarla sin Windows de por medio.
/// </summary>
public static class SystemAccentColor
{
    public static string ToHex(uint packedAbgr)
    {
        var r = packedAbgr & 0xFF;
        var g = (packedAbgr >> 8) & 0xFF;
        var b = (packedAbgr >> 16) & 0xFF;
        return $"#{r:X2}{g:X2}{b:X2}";
    }
}
