using Nexo.Core.Audit;
using Nexo.Core.Diagnostics;
using Nexo.Core.Memory;
using Nexo.Core.Productization;
using Nexo.Core.SelfCheck;
using Nexo.Core.Settings;
using Nexo.Windows.Audit;
using Nexo.Windows.Memory;
using Nexo.Windows.Productization;
using Nexo.Windows.Settings;

namespace Nexo.Windows.SelfCheck;

/// <summary>
/// Diseño D22 — las comprobaciones que sí tocan disco: guardar y releer ajustes, escribir en el
/// registro, cifrar y descifrar la memoria, y copiar y verificar.
///
/// **Todas corren en una carpeta temporal propia, nunca sobre los datos reales.** Es la misma razón
/// por la que existe el perfil de validación de D3.2: una comprobación que escribiera en el perfil
/// de la persona mezclaría sus datos con los de la prueba, y una autocomprobación que ensucia lo que
/// comprueba no sirve de nada. La carpeta se borra al terminar.
///
/// Cada comprobación aísla su fallo: si el cifrado no funciona en este equipo, el resto se ejecuta
/// igual. Un informe que se detiene en el primer fallo esconde los demás, que es justo lo que no
/// interesa cuando alguien está intentando entender por qué algo no va.
/// </summary>
public sealed class WindowsSelfCheckProbes
{
    public IReadOnlyList<SelfCheckResult> Run()
    {
        var sandbox = Path.Combine(
            Path.GetTempPath(), "kohana-autocomprobacion", Guid.NewGuid().ToString("N"));

        try
        {
            Directory.CreateDirectory(sandbox);

            return
            [
                CheckSettingsRoundTrip(sandbox),
                CheckAuditLogWrites(sandbox),
                CheckMemoryEncryption(sandbox),
                CheckBackupVerification(sandbox),
                CheckDataFolderIsWritable()
            ];
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
            return
            [
                SelfCheckResult.Skipped(
                    "entorno",
                    "Comprobaciones sobre disco",
                    "Que guardar, cifrar y copiar funcionan en este equipo.",
                    $"No pude crear una carpeta temporal para comprobarlo: {exception.Message}")
            ];
        }
        finally
        {
            TryDelete(sandbox);
        }
    }

    private static SelfCheckResult CheckSettingsRoundTrip(string sandbox)
    {
        const string id = "disco.ajustes";
        const string title = "Tus ajustes se guardan y se releen";
        const string proves = "Que lo que configuras sobrevive a cerrar y abrir Sakura.";

        try
        {
            var path = Path.Combine(sandbox, "settings.json");
            var store = new JsonSettingsStore(path);

            var original = new ShellPreferences { AccentColor = "#ABCDEF", Width = 720 };
            store.Save(original);

            var reloaded = store.Load();

            return reloaded.AccentColor == "#ABCDEF" && Math.Abs(reloaded.Width - 720) < 0.5
                ? SelfCheckResult.Ok(id, title, proves)
                : SelfCheckResult.Fail(id, title, proves, "Lo guardado no coincide con lo releído.");
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
            return SelfCheckResult.Fail(id, title, proves, exception.Message);
        }
    }

    private static SelfCheckResult CheckAuditLogWrites(string sandbox)
    {
        const string id = "disco.auditoria";
        const string title = "El registro de actividad se escribe";
        const string proves = "Que lo que Sakura hace queda anotado y se puede releer después.";

        try
        {
            var log = new JsonSakuraAuditLog(
                Path.Combine(sandbox, "audit.json"),
                Path.Combine(sandbox, "optimization-audit.json"));

            log.Append(new AuditEntry
            {
                At = DateTimeOffset.Now,
                Capability = AuditCapability.Permisos,
                Action = "Autocomprobación",
                Detail = "Escritura de prueba",
                Permission = "Comprobación interna"
            });

            return log.Read().Count == 1
                ? SelfCheckResult.Ok(id, title, proves)
                : SelfCheckResult.Fail(id, title, proves, "La entrada escrita no se pudo releer.");
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
            return SelfCheckResult.Fail(id, title, proves, exception.Message);
        }
    }

    private static SelfCheckResult CheckMemoryEncryption(string sandbox)
    {
        const string id = "disco.memoria";
        const string title = "La memoria se guarda cifrada";
        const string proves =
            "Que lo que Sakura recuerda no se puede leer abriendo el archivo, y que sí se puede " +
            "recuperar desde tu cuenta.";

        try
        {
            var path = Path.Combine(sandbox, "memory.dat");
            var store = new DpapiMemoryStore(path);

            const string secret = "frase-de-comprobacion-kohana";
            store.Save(new MemoryState
            {
                Entries =
                [
                    new MemoryEntry
                    {
                        Category = MemoryCategory.Preferencias,
                        Text = secret,
                        CreatedAt = DateTimeOffset.Now
                    }
                ]
            });

            if (!File.Exists(path))
            {
                return SelfCheckResult.Fail(id, title, proves, "No se escribió el archivo de memoria.");
            }

            // La comprobación que importa: el texto NO debe verse en el archivo.
            var raw = File.ReadAllText(path);
            if (raw.Contains(secret, StringComparison.OrdinalIgnoreCase))
            {
                return SelfCheckResult.Fail(id, title, proves,
                    "El texto se puede leer en el archivo: no está cifrado.");
            }

            var reloaded = store.Load();

            return reloaded.Entries.Any(entry => entry.Text == secret)
                ? SelfCheckResult.Ok(id, title, proves, "Cifrado en disco y recuperable desde tu cuenta.")
                : SelfCheckResult.Fail(id, title, proves, "Cifró pero no pudo descifrar.");
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or
                System.Security.Cryptography.CryptographicException)
        {
            return SelfCheckResult.Fail(id, title, proves, exception.Message);
        }
    }

    private static SelfCheckResult CheckBackupVerification(string sandbox)
    {
        const string id = "disco.copia";
        const string title = "La copia de seguridad se verifica de verdad";
        const string proves =
            "Que antes de actualizar se copia tu configuración y se comprueba comparando el contenido.";

        try
        {
            var dataRoot = Path.Combine(sandbox, "datos");
            Directory.CreateDirectory(dataRoot);
            File.WriteAllText(Path.Combine(dataRoot, "settings.json"), "{ \"comprobacion\": true }");

            var service = new FileSystemDataBackupService(
                Path.Combine(sandbox, "backups"), dataRoot);

            var item = SakuraDataInventory.Find("settings.json")!;
            var result = service.CreateBackup([item]);

            var outcome = result.Files.FirstOrDefault();

            return result.Success && outcome is { Copied: true, Verified: true }
                ? SelfCheckResult.Ok(id, title, proves)
                : SelfCheckResult.Fail(id, title, proves, result.Detail);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
            return SelfCheckResult.Fail(id, title, proves, exception.Message);
        }
    }

    private static SelfCheckResult CheckDataFolderIsWritable()
    {
        const string id = "disco.carpeta";
        const string title = "Sakura puede escribir en su carpeta de datos";
        const string proves = "Que la carpeta real donde vive todo no está bloqueada ni llena.";

        try
        {
            Directory.CreateDirectory(NexoDataPaths.RootDirectory);

            // Nombre propio y borrado inmediato: la comprobación no debe dejar rastro en la carpeta
            // de la persona, ni siquiera un archivo vacío.
            var probe = Path.Combine(NexoDataPaths.RootDirectory, ".kohana-autocomprobacion");
            File.WriteAllText(probe, "ok");
            File.Delete(probe);

            return SelfCheckResult.Ok(id, title, proves,
                PathRedactor.Shorten(NexoDataPaths.RootDirectory));
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
            return SelfCheckResult.Fail(id, title, proves,
                $"No se pudo escribir: {exception.Message}");
        }
    }

    private static void TryDelete(string directory)
    {
        try
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
            // La carpeta está en el temporal del sistema: si no se puede borrar ahora, Windows la
            // limpiará. No merece ensuciar el informe con esto.
        }
    }
}
