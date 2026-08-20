using System.IO;

namespace Nexo.Core.Updates;

/// <summary>Las tres carpetas del intercambio, o por qué no se puede hacer.</summary>
public readonly record struct UpdateSwapPaths(
    bool IsSafe,
    string Install,
    string Staged,
    string Previous,
    string Problem)
{
    public static UpdateSwapPaths Refuse(string problem) =>
        new(false, string.Empty, string.Empty, string.Empty, problem);
}

/// <summary>
/// Diseño D63 — decide qué carpetas se mueven, y se niega cuando la respuesta sería peligrosa.
///
/// Esta clase existe por una razón concreta: el intercambio **aparta una carpeta y borra otra**, y
/// un error aquí no da un fallo, da una pérdida. Si la carpeta de instalación resultara ser
/// <c>C:\</c>, el escritorio, o la carpeta de documentos de alguien, el actualizador la apartaría
/// entera. No hay reintento que arregle eso, así que se comprueba antes y se dice que no.
///
/// Es el mismo razonamiento de <see cref="Nexo.Core.Workspace.WorkspaceRootPolicy"/> —que nació de
/// autorizar <c>C:\</c> y colgar la aplicación— aplicado a algo bastante menos perdonable: allí se
/// leía de más, aquí se movería de más.
///
/// **Las tres carpetas son hermanas y del mismo disco.** Mover una carpeta dentro del mismo volumen
/// es una operación de un instante que no copia nada; entre discos, Windows la convierte en copiar
/// y borrar, que con quinientos archivos tarda y puede cortarse a mitad — exactamente el estado que
/// todo este diseño trata de evitar.
/// </summary>
public static class UpdateSwapPathPolicy
{
    /// <summary>Sufijos de las carpetas de trabajo, al lado de la instalación.</summary>
    private const string StagedSuffix = ".new";
    private const string PreviousSuffix = ".old";

    /// <summary>
    /// Lo menos que puede tener una ruta de instalación para no ser un sitio compartido: una unidad
    /// y al menos dos carpetas, como <c>…\Programs\Sakura</c>.
    /// </summary>
    private const int MinimumDepth = 2;

    public static UpdateSwapPaths Resolve(string? installFolder)
    {
        if (string.IsNullOrWhiteSpace(installFolder))
        {
            return UpdateSwapPaths.Refuse("No se sabe dónde está instalada Sakura.");
        }

        var raw = installFolder.Trim();

        // Una letra de unidad a secas NO es la raíz: Windows guarda un directorio actual por
        // unidad, así que «C:» se resuelve a lo que sea que esté en curso en C: — hoy una cosa y
        // mañana otra. Aceptarlo significaría apartar una carpeta elegida por el azar del proceso.
        // Se rechaza antes de resolverla, porque después ya parece una ruta normal y corriente.
        if (raw.Length == 2 && raw[1] == ':' && char.IsLetter(raw[0]))
        {
            return UpdateSwapPaths.Refuse(
                "Una letra de unidad sola no dice una carpeta: apunta a la que esté en curso.");
        }

        string full;
        try
        {
            full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(raw));
        }
        catch (Exception exception) when (
            exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return UpdateSwapPaths.Refuse("La ruta de instalación no se puede interpretar.");
        }

        var root = Path.GetPathRoot(full);
        if (string.IsNullOrEmpty(root))
        {
            return UpdateSwapPaths.Refuse("La instalación tiene que estar en una unidad concreta.");
        }

        // La raíz de un disco no es una instalación: apartarla sería apartar todo lo demás.
        if (string.Equals(
                Path.TrimEndingDirectorySeparator(root),
                full,
                StringComparison.OrdinalIgnoreCase))
        {
            return UpdateSwapPaths.Refuse("La raíz de una unidad no puede ser la carpeta de Sakura.");
        }

        var parent = Path.GetDirectoryName(full);
        if (string.IsNullOrEmpty(parent))
        {
            return UpdateSwapPaths.Refuse("La instalación tiene que estar dentro de otra carpeta.");
        }

        if (Depth(full, root) < MinimumDepth)
        {
            return UpdateSwapPaths.Refuse(
                "Esa carpeta está demasiado arriba para ser una instalación de Sakura.");
        }

        // Carpetas conocidas del perfil: nadie instala en el escritorio, pero un ajuste mal escrito
        // o una ruta heredada sí pueden acabar apuntando ahí, y el precio de acertar es cero.
        if (IsWellKnownFolder(full))
        {
            return UpdateSwapPaths.Refuse(
                "Esa es una carpeta personal de Windows, no una instalación.");
        }

        return new UpdateSwapPaths(
            IsSafe: true,
            Install: full,
            Staged: full + StagedSuffix,
            Previous: full + PreviousSuffix,
            Problem: string.Empty);
    }

    /// <summary>Cuántas carpetas cuelgan de la raíz. <c>C:\Sakura</c> es 1; <c>C:\a\Sakura</c> es 2.</summary>
    private static int Depth(string full, string root) =>
        full[root.Length..]
            .Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
                StringSplitOptions.RemoveEmptyEntries)
            .Length;

    private static bool IsWellKnownFolder(string full)
    {
        Environment.SpecialFolder[] folders =
        [
            Environment.SpecialFolder.UserProfile,
            Environment.SpecialFolder.DesktopDirectory,
            Environment.SpecialFolder.MyDocuments,
            Environment.SpecialFolder.MyPictures,
            Environment.SpecialFolder.MyMusic,
            Environment.SpecialFolder.MyVideos,
            Environment.SpecialFolder.Windows,
            Environment.SpecialFolder.System,
            Environment.SpecialFolder.ProgramFiles,
            Environment.SpecialFolder.ProgramFilesX86,
            Environment.SpecialFolder.LocalApplicationData,
            Environment.SpecialFolder.ApplicationData
        ];

        foreach (var folder in folders)
        {
            var path = Environment.GetFolderPath(folder);
            if (string.IsNullOrEmpty(path))
            {
                continue;
            }

            if (string.Equals(
                    Path.TrimEndingDirectorySeparator(Path.GetFullPath(path)),
                    full,
                    StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Si dos rutas están en el mismo volumen. Se comprueba antes de mover porque mover dentro de un
    /// volumen es instantáneo y entre volúmenes es copiar y borrar, que con quinientos archivos
    /// tarda y puede cortarse a mitad.
    /// </summary>
    public static bool SameVolume(string left, string right)
    {
        var leftRoot = Path.GetPathRoot(Path.GetFullPath(left));
        var rightRoot = Path.GetPathRoot(Path.GetFullPath(right));

        return !string.IsNullOrEmpty(leftRoot) &&
               string.Equals(leftRoot, rightRoot, StringComparison.OrdinalIgnoreCase);
    }
}
