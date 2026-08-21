using System.Text.Json;
using Nexo.Core.Branding;
using Nexo.Core.Diagnostics;

namespace Nexo.Windows.Storage;

/// <summary>
/// Copia de forma conservadora los datos de la etapa Nexo hacia Sakura.
/// Nunca elimina ni sobrescribe el origen y puede ejecutarse más de una vez.
/// </summary>
public static class LegacyDataMigrator
{
    private const string MarkerFileName = ".migrated-from-nexo-v1.json";

    private static readonly HashSet<string> ExcludedTopLevelDirectories =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "Logs",
            "Temp",
            "Models",
            "Runtime"
        };

    public static LegacyDataMigrationResult MigrateIfNeeded() => Migrate(
        NexoDataPaths.LegacyRootDirectory,
        NexoDataPaths.RootDirectory);

    /// <summary>
    /// Diseño D70 — trae los modelos de voz y el runtime de IA a la carpeta actual.
    ///
    /// La migración normal los deja fuera a propósito: son gigas y copiarlos alargaría el primer
    /// arranque. El precio de dejarlos fuera resultó ser peor de lo previsto: en el equipo de Adler
    /// seguían en la carpeta de la etapa Nexo —tres giga y medio de modelos, casi dos de Ollama—,
    /// leídos en vivo desde ahí, así que borrar la carpeta vieja habría dejado a la aplicación sin
    /// voz sin que nada avisara.
    ///
    /// Aquí se **mueven**, no se copian: dentro del mismo volumen mover una carpeta es renombrarla,
    /// así que cuesta lo mismo con tres gigas que con tres kilobytes y no hace falta el doble de
    /// disco libre. Si el destino ya existe no se toca nada, y si el movimiento falla —otro volumen,
    /// un archivo abierto— se deja como estaba: la búsqueda en cadena sigue encontrándolos donde
    /// están y nadie se queda sin nada.
    /// </summary>
    public static IReadOnlyList<string> ConsolidateHeavyFolders()
    {
        var moved = new List<string>();

        if (NexoDataPaths.IsUsingOverrideRoot)
        {
            return moved;
        }

        foreach (var folder in new[] { "Models", "Runtime" })
        {
            var destination = Path.Combine(NexoDataPaths.RootDirectory, folder);
            if (Directory.Exists(destination))
            {
                continue;
            }

            foreach (var legacyRoot in NexoDataPaths.LegacyRootDirectories)
            {
                var source = Path.Combine(legacyRoot, folder);
                if (!Directory.Exists(source))
                {
                    continue;
                }

                try
                {
                    Directory.CreateDirectory(NexoDataPaths.RootDirectory);
                    Directory.Move(source, destination);
                    moved.Add($"{source} → {destination}");
                }
                catch (Exception exception) when (
                    exception is IOException or UnauthorizedAccessException)
                {
                    // Se queda donde está y se sigue leyendo desde ahí.
                }

                break;
            }
        }

        return moved;
    }

    /// <summary>
    /// Sobrecarga explícita para poder comprobar la migración sin depender de
    /// las carpetas reales del usuario.
    /// </summary>
    public static LegacyDataMigrationResult Migrate(
        string sourceRoot,
        string destinationRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationRoot);

        sourceRoot = Path.GetFullPath(sourceRoot);
        destinationRoot = Path.GetFullPath(destinationRoot);
        var markerPath = Path.Combine(destinationRoot, MarkerFileName);

        if (sourceRoot.Equals(destinationRoot, StringComparison.OrdinalIgnoreCase) ||
            !Directory.Exists(sourceRoot))
        {
            return LegacyDataMigrationResult.NotNeeded();
        }

        Directory.CreateDirectory(destinationRoot);
        if (File.Exists(markerPath))
        {
            return LegacyDataMigrationResult.AlreadyCompleted(markerPath);
        }

        var copiedFiles = 0;
        var skippedFiles = 0;
        var failures = new List<string>();
        IReadOnlyList<string> sourceFiles;

        try
        {
            sourceFiles = Directory
                .EnumerateFiles(sourceRoot, "*", SearchOption.AllDirectories)
                .ToArray();
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
            return new LegacyDataMigrationResult(
                WasNeeded: true,
                WasAlreadyCompleted: false,
                CopiedFiles: 0,
                SkippedFiles: 0,
                FailedFiles: 1,
                MarkerPath: markerPath);
        }

        foreach (var sourceFile in sourceFiles)
        {
            var relativePath = Path.GetRelativePath(sourceRoot, sourceFile);
            var pathSegments = relativePath.Split(
                [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
                StringSplitOptions.RemoveEmptyEntries);
            var firstSegment = pathSegments.FirstOrDefault() ?? string.Empty;

            if (ExcludedTopLevelDirectories.Contains(firstSegment))
            {
                continue;
            }

            var destinationFile = Path.Combine(destinationRoot, relativePath);
            if (File.Exists(destinationFile))
            {
                skippedFiles++;
                continue;
            }

            try
            {
                var destinationDirectory = Path.GetDirectoryName(destinationFile);
                if (!string.IsNullOrWhiteSpace(destinationDirectory))
                {
                    Directory.CreateDirectory(destinationDirectory);
                }

                File.Copy(sourceFile, destinationFile, overwrite: false);
                File.SetLastWriteTimeUtc(
                    destinationFile,
                    File.GetLastWriteTimeUtc(sourceFile));
                copiedFiles++;
            }
            catch (Exception exception) when (
                exception is IOException or UnauthorizedAccessException)
            {
                failures.Add(relativePath);
            }
        }

        // Si algún archivo no pudo copiarse, se omite el marcador. La próxima
        // ejecución reintentará únicamente lo que todavía falte.
        if (failures.Count == 0)
        {
            var marker = new
            {
                product = ProductIdentity.ProductName,
                previousProduct = ProductIdentity.PreviousProductName,
                migratedAtUtc = DateTimeOffset.UtcNow,
                copiedFiles,
                skippedFiles,
                failedFiles = 0
            };

            try
            {
                File.WriteAllText(
                    markerPath,
                    JsonSerializer.Serialize(
                        marker,
                        new JsonSerializerOptions { WriteIndented = true }));
            }
            catch (Exception exception) when (
                exception is IOException or UnauthorizedAccessException)
            {
                failures.Add(MarkerFileName);
            }
        }

        return new LegacyDataMigrationResult(
            WasNeeded: true,
            WasAlreadyCompleted: false,
            CopiedFiles: copiedFiles,
            SkippedFiles: skippedFiles,
            FailedFiles: failures.Count,
            MarkerPath: markerPath);
    }
}

public sealed record LegacyDataMigrationResult(
    bool WasNeeded,
    bool WasAlreadyCompleted,
    int CopiedFiles,
    int SkippedFiles,
    int FailedFiles,
    string MarkerPath)
{
    public bool Succeeded => FailedFiles == 0;

    public static LegacyDataMigrationResult NotNeeded() =>
        new(false, false, 0, 0, 0, string.Empty);

    public static LegacyDataMigrationResult AlreadyCompleted(string markerPath) =>
        new(true, true, 0, 0, 0, markerPath);
}
