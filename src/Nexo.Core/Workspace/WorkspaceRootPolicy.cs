namespace Nexo.Core.Workspace;

/// <summary>Si una carpeta puede autorizarse como proyecto, y por qué no si no.</summary>
public readonly record struct WorkspaceRootVerdict(bool IsAllowed, string Message)
{
    public static WorkspaceRootVerdict Allow() => new(true, string.Empty);

    public static WorkspaceRootVerdict Refuse(string message) => new(false, message);
}

/// <summary>
/// Diseño D41 — qué carpetas tiene sentido autorizar como proyecto.
///
/// Nace de un fallo real: se autorizó <c>C:\</c> y Sakura se quedó sin responder. La causa inmediata
/// era de rendimiento —recorrer un disco entero—, pero el fondo es de diseño: el permiso de proyecto
/// existe para acotar hasta dónde llega Sakura, y autorizar la raíz del disco es exactamente no
/// acotar nada. Un tope de archivos no arregla eso; solo hace que el mismo permiso desmedido tarde
/// menos.
///
/// Se rechazan tres cosas distintas por tres motivos distintos:
///
/// La raíz de una unidad no es un proyecto. Nadie tiene un proyecto en <c>C:\</c>: lo que hay ahí es
/// todo lo demás.
///
/// Las carpetas del sistema y de programas contienen archivos que no son de quien usa el equipo, y
/// que además cambian solos. Leerlas no ayuda a nada y expone lo que sea que haya instalado.
///
/// La raíz del perfil —<c>C:\Users\Alguien</c>— es la trampa sutil: parece «mis cosas», pero incluye
/// Descargas, Escritorio, Documentos y toda la configuración de cada programa instalado. Es casi tan
/// amplio como el disco entero, con la diferencia de que ahí sí hay datos personales.
/// </summary>
public static class WorkspaceRootPolicy
{
    public static WorkspaceRootVerdict CanAuthorize(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return WorkspaceRootVerdict.Refuse("No elegiste ninguna carpeta.");
        }

        string full;
        try
        {
            full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path.Trim()));
        }
        catch (Exception exception) when (
            exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return WorkspaceRootVerdict.Refuse("Esa ruta no es válida.");
        }

        if (IsDriveRoot(full))
        {
            return WorkspaceRootVerdict.Refuse(
                "Esa es la raíz de una unidad entera, no una carpeta de proyecto. " +
                "Elige la carpeta concreta en la que estés trabajando.");
        }

        if (IsProfileRoot(full))
        {
            return WorkspaceRootVerdict.Refuse(
                "Esa es la carpeta de tu usuario completa: incluye Descargas, Escritorio, " +
                "Documentos y la configuración de todos tus programas. Elige la carpeta concreta " +
                "del proyecto.");
        }

        if (SystemFolder(full) is { } systemFolder)
        {
            return WorkspaceRootVerdict.Refuse(
                $"«{systemFolder}» es una carpeta del sistema. Ahí no hay nada tuyo que Sakura " +
                "deba leer.");
        }

        return WorkspaceRootVerdict.Allow();
    }

    private static bool IsDriveRoot(string full)
    {
        var root = Path.GetPathRoot(full);
        return !string.IsNullOrEmpty(root) &&
               string.Equals(
                   Path.TrimEndingDirectorySeparator(root),
                   full,
                   StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// La raíz del perfil, y también la carpeta que las contiene a todas. Se comparan rutas ya
    /// normalizadas y sin distinguir mayúsculas, que es como se comportan las rutas de Windows.
    /// </summary>
    private static bool IsProfileRoot(string full)
    {
        foreach (var folder in new[]
                 {
                     Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                     Path.GetDirectoryName(
                         Environment.GetFolderPath(Environment.SpecialFolder.UserProfile))
                 })
        {
            if (string.IsNullOrWhiteSpace(folder))
            {
                continue;
            }

            if (string.Equals(
                    Path.TrimEndingDirectorySeparator(Path.GetFullPath(folder)),
                    full,
                    StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Devuelve el nombre de la carpeta de sistema que contiene la ruta, o <c>null</c>. Se comprueba
    /// que sea la carpeta o algo dentro de ella —comparando con el separador— para que
    /// <c>C:\Windows-mis-cosas</c> no cuente como dentro de <c>C:\Windows</c>.
    /// </summary>
    private static string? SystemFolder(string full)
    {
        foreach (var special in new[]
                 {
                     Environment.SpecialFolder.Windows,
                     Environment.SpecialFolder.ProgramFiles,
                     Environment.SpecialFolder.ProgramFilesX86,
                     Environment.SpecialFolder.CommonApplicationData,
                     Environment.SpecialFolder.System
                 })
        {
            var folder = Environment.GetFolderPath(special);
            if (string.IsNullOrWhiteSpace(folder))
            {
                continue;
            }

            var normalized = Path.TrimEndingDirectorySeparator(Path.GetFullPath(folder));

            if (string.Equals(normalized, full, StringComparison.OrdinalIgnoreCase) ||
                full.StartsWith(normalized + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            {
                return normalized;
            }
        }

        return null;
    }
}
