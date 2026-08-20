using System.Security.Cryptography;
using Nexo.Core.Diagnostics;
using Nexo.Core.Productization;

namespace Nexo.Windows.Productization;

/// <summary>
/// Diseño D20 (Fase 9) — copia los datos de Sakura a una carpeta con fecha, y los devuelve.
///
/// **Verificar es comparar el contenido, no el tamaño.** Se comparan los hashes SHA-256 del origen y
/// del destino: dos archivos del mismo tamaño pueden ser distintos, y una copia que solo comprueba
/// el tamaño da por buena justo la corrupción que más se parece a un archivo sano.
///
/// Las copias no se borran solas. Ocupan poco (son JSON y un archivo cifrado pequeño) y el momento
/// en que hacen falta es siempre después de que algo saliera mal, que es el peor momento para
/// descubrir que se limpiaron por antigüedad.
/// </summary>
public sealed class FileSystemDataBackupService : IDataBackupService
{
    private readonly string _root;
    private readonly string? _dataRoot;
    private readonly Func<DateTimeOffset> _clock;

    /// <summary>
    /// <paramref name="dataRoot"/> permite copiar desde una carpeta concreta en vez de la global,
    /// igual que el resto de stores del proyecto aceptan una ruta explícita. No es solo comodidad
    /// para las pruebas: sin esto, probar esta clase obligaría a mutar
    /// <see cref="NexoDataPaths"/> global, y hay otra prueba del mismo ensamblado que ya lo hace y
    /// documenta que nadie más debería.
    /// </summary>
    public FileSystemDataBackupService(
        string? backupRoot = null,
        string? dataRoot = null,
        Func<DateTimeOffset>? clock = null)
    {
        _dataRoot = dataRoot;
        _root = backupRoot ?? Path.Combine(dataRoot ?? NexoDataPaths.RootDirectory, "backups");
        _clock = clock ?? (() => DateTimeOffset.Now);
    }

    private string SourcePathOf(SakuraDataItem item) =>
        _dataRoot is null ? item.FullPath : Path.Combine(_dataRoot, item.FileName);

    public BackupResult CreateBackup(IReadOnlyList<SakuraDataItem> items)
    {
        ArgumentNullException.ThrowIfNull(items);

        var backupId = _clock().ToString("yyyyMMdd-HHmmss");
        var destination = Path.Combine(_root, backupId);

        try
        {
            Directory.CreateDirectory(destination);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
            return BackupResult.Failed($"No pude crear la carpeta de copia: {exception.Message}");
        }

        var outcomes = new List<BackupFileOutcome>();

        foreach (var item in items)
        {
            outcomes.Add(CopyIntoBackup(SourcePathOf(item), item.FileName, destination));
        }

        var broken = outcomes.Where(outcome => outcome.Copied && !outcome.Verified).ToArray();
        if (broken.Length > 0)
        {
            return new BackupResult(
                false,
                backupId,
                $"{broken.Length} archivos no se pudieron verificar tras copiarlos.",
                outcomes);
        }

        return new BackupResult(
            true,
            backupId,
            $"Copia guardada en {destination}.",
            outcomes);
    }

    public BackupResult Restore(string backupId)
    {
        if (string.IsNullOrWhiteSpace(backupId))
        {
            return BackupResult.Failed("No me dijiste qué copia restaurar.");
        }

        var source = Path.Combine(_root, backupId);
        if (!Directory.Exists(source))
        {
            return BackupResult.Failed($"No encuentro la copia «{backupId}».");
        }

        var outcomes = new List<BackupFileOutcome>();

        foreach (var item in SakuraDataInventory.All)
        {
            var backedUp = Path.Combine(source, item.FileName);
            if (!File.Exists(backedUp))
            {
                // No estaba cuando se copió, así que no debe aparecer al restaurar. Restaurar es
                // volver a como estaba, no rellenar huecos.
                outcomes.Add(new BackupFileOutcome(item.FileName, false, false, "No estaba en la copia."));
                continue;
            }

            outcomes.Add(CopyAndVerify(backedUp, SourcePathOf(item), item.FileName));
        }

        var broken = outcomes.Where(outcome => outcome.Copied && !outcome.Verified).ToArray();

        return broken.Length > 0
            ? new BackupResult(false, backupId,
                $"{broken.Length} archivos no quedaron verificados al restaurar.", outcomes)
            : new BackupResult(true, backupId,
                $"Restauré {outcomes.Count(outcome => outcome.Copied)} archivos de la copia «{backupId}».",
                outcomes);
    }

    public IReadOnlyList<string> ListBackups()
    {
        try
        {
            return Directory.Exists(_root)
                ? [.. Directory.GetDirectories(_root).Select(Path.GetFileName).OfType<string>().OrderDescending()]
                : [];
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    private static BackupFileOutcome CopyIntoBackup(
        string sourcePath,
        string fileName,
        string destinationDirectory)
    {
        if (!File.Exists(sourcePath))
        {
            // Quien nunca activó la memoria no tiene memoria que copiar. No es un fallo.
            return new BackupFileOutcome(fileName, false, false, "No existe todavía.");
        }

        return CopyAndVerify(sourcePath, Path.Combine(destinationDirectory, fileName), fileName);
    }

    private static BackupFileOutcome CopyAndVerify(string source, string destination, string fileName)
    {
        try
        {
            var directory = Path.GetDirectoryName(destination);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.Copy(source, destination, overwrite: true);

            var verified = HashOf(source).SequenceEqual(HashOf(destination));

            return new BackupFileOutcome(
                fileName,
                true,
                verified,
                verified ? "Copiado y verificado." : "Copiado, pero el contenido no coincide.");
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
            return new BackupFileOutcome(fileName, false, false, $"No se pudo copiar: {exception.Message}");
        }
    }

    private static byte[] HashOf(string path)
    {
        using var stream = File.OpenRead(path);
        return SHA256.HashData(stream);
    }
}
