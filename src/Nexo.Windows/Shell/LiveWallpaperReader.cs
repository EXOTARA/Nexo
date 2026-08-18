using System.IO;
using Microsoft.Win32;
using Nexo.Core.Shell;

namespace Nexo.Windows.Shell;

/// <summary>
/// Diseño D32 — localiza en disco la miniatura del fondo que Wallpaper Engine tiene puesto ahora.
/// El porqué de existir está en <see cref="LiveWallpaperConfig"/>; aquí solo está lo que toca disco
/// y registro, separado para que la lógica se pueda probar sin tener el programa instalado.
/// </summary>
public static class LiveWallpaperReader
{
    /// <summary>
    /// Los procesos de Wallpaper Engine. Que esté instalado no basta: su <c>config.json</c> conserva
    /// el último fondo elegido aunque el programa esté cerrado, y entonces Windows vuelve a mostrar
    /// su propio fondo. Sin esta comprobación Kohana se pintaba con el color de un fondo que nadie
    /// estaba viendo — se detectó en pantalla, con el programa cerrado y el tema equivocado puesto.
    /// </summary>
    private static readonly string[] ProcessNames = ["wallpaper32", "wallpaper64"];

    private static bool IsRunning()
    {
        foreach (var name in ProcessNames)
        {
            try
            {
                if (System.Diagnostics.Process.GetProcessesByName(name).Length > 0)
                {
                    return true;
                }
            }
            catch (InvalidOperationException)
            {
                // Un proceso que termina mientras se consulta: cuenta como no encontrado.
            }
        }

        return false;
    }

    public static string? TryFindPreviewImage()
    {
        if (!IsRunning())
        {
            return null;
        }

        foreach (var configPath in CandidateConfigPaths())
        {
            string json;
            try
            {
                // FileShare.ReadWrite: Wallpaper Engine puede tener su propio config abierto.
                using var stream = new FileStream(
                    configPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var reader = new StreamReader(stream);
                json = reader.ReadToEnd();
            }
            catch (Exception exception) when (
                exception is IOException or UnauthorizedAccessException or System.Security.SecurityException)
            {
                continue;
            }

            foreach (var wallpaperPath in LiveWallpaperConfig.ReadSelectedWallpapers(json))
            {
                if (LiveWallpaperConfig.PreviewFolderOf(wallpaperPath) is not { } folder)
                {
                    continue;
                }

                foreach (var name in LiveWallpaperConfig.PreviewFileNames)
                {
                    var preview = Path.Combine(folder, name);
                    if (File.Exists(preview))
                    {
                        return preview;
                    }
                }
            }
        }

        return null;
    }

    private static IEnumerable<string> CandidateConfigPaths()
    {
        foreach (var installFolder in InstallFolders())
        {
            var config = Path.Combine(installFolder, "config.json");
            if (File.Exists(config))
            {
                yield return config;
            }
        }
    }

    private static IEnumerable<string> InstallFolders()
    {
        // La ruta de Steam sale del registro y no de una constante: Steam se instala a menudo en un
        // disco secundario, y dar por hecho "Program Files (x86)" dejaría fuera justo a quien tiene
        // la biblioteca en otro sitio.
        string? steamPath = null;
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam");
            steamPath = key?.GetValue("SteamPath") as string;
        }
        catch (Exception exception) when (
            exception is System.Security.SecurityException or UnauthorizedAccessException or IOException)
        {
            // Sin registro queda la ruta por defecto de abajo.
        }

        if (!string.IsNullOrWhiteSpace(steamPath))
        {
            yield return Path.Combine(
                steamPath.Replace('/', Path.DirectorySeparatorChar),
                "steamapps", "common", "wallpaper_engine");
        }

        yield return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            "Steam", "steamapps", "common", "wallpaper_engine");
    }
}
