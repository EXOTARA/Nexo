using System.Text.Json;
using Nexo.Core.Branding;
using Nexo.Core.Diagnostics;

namespace Nexo.Windows.Storage;

/// <summary>
/// Copia de forma conservadora los datos de la etapa Nexo hacia Kohana.
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
