using Nexo.Core.Audit;

namespace Nexo.Core.Productization;

/// <summary>Diseño D20 — cómo fue la copia de un archivo. Por archivo, no por lote.</summary>
public sealed record BackupFileOutcome(string FileName, bool Copied, bool Verified, string Detail);

public sealed record BackupResult(
    bool Success,
    string BackupId,
    string Detail,
    IReadOnlyList<BackupFileOutcome> Files)
{
    public static BackupResult Failed(string detail, IReadOnlyList<BackupFileOutcome>? files = null) =>
        new(false, string.Empty, detail, files ?? []);
}

/// <summary>
/// Diseño D20 — copia y restaura los datos de Kohana. **Verificar es releer**, igual que en las
/// Fases 4, 5 y 7: una copia que no se ha comprobado no es una copia, es una intención.
/// </summary>
public interface IDataBackupService
{
    BackupResult CreateBackup(IReadOnlyList<KohanaDataItem> items);

    BackupResult Restore(string backupId);

    IReadOnlyList<string> ListBackups();
}

public sealed record UpdateReadiness(bool CanUpdate, string Detail, BackupResult? Backup)
{
    public static UpdateReadiness Blocked(string detail, BackupResult? backup = null) =>
        new(false, detail, backup);
}

/// <summary>
/// Diseño D20 (Fase 9) — prepara una actualización. El roadmap marca el riesgo de esta fase sin
/// rodeos: *"un actualizador mal diseñado puede romper instalaciones existentes — requiere el mismo
/// rigor de reversibilidad que la Fase 4"*. Así que se aplica el mismo patrón que allí, y por el
/// mismo motivo:
///
/// 1. **La copia se hace ANTES**, no durante.
/// 2. **Se verifica releyendo cada archivo.** Que copiar no diera error no significa que el archivo
///    esté completo al otro lado.
/// 3. **Si algún archivo no queda verificado, la actualización NO se declara segura.** No es
///    prudencia de más: el único momento en que la copia importa es cuando algo salió mal, y ahí ya
///    no hay ocasión de comprobarla.
/// 4. **Queda en el Audit Log**, con cómo volver atrás.
///
/// No actualiza nada: eso lo hace el instalador. Lo que esta clase garantiza es que, si se actualiza,
/// haya una copia verificada de la que volver.
/// </summary>
public sealed class UpdateSafetyCoordinator
{
    private readonly IDataBackupService _backups;
    private readonly IAuditLog _audit;
    private readonly Func<DateTimeOffset> _clock;

    public UpdateSafetyCoordinator(
        IDataBackupService backups,
        IAuditLog audit,
        Func<DateTimeOffset>? clock = null)
    {
        _backups = backups;
        _audit = audit;
        _clock = clock ?? (() => DateTimeOffset.Now);
    }

    public UpdateReadiness PrepareUpdate()
    {
        var backup = _backups.CreateBackup(KohanaDataInventory.ToBackUp);

        var unverified = backup.Files.Where(file => file.Copied && !file.Verified).ToArray();
        var failed = backup.Files.Where(file => !file.Copied).ToArray();

        if (!backup.Success || unverified.Length > 0)
        {
            var detail = unverified.Length > 0
                ? $"Copié tus datos pero {unverified.Length} archivos no quedaron verificados, " +
                  "así que no doy la actualización por segura."
                : backup.Detail;

            Record("Actualización no preparada", detail, string.Empty);
            return UpdateReadiness.Blocked(detail, backup);
        }

        // Un archivo que no existe no es un fallo: quien nunca activó la memoria no tiene memoria
        // que copiar. Lo que sí sería un fallo es que existiera y no se copiara.
        var copied = backup.Files.Count(file => file.Copied);

        var message = failed.Length == 0
            ? $"Copia verificada de {copied} archivos. Puedes actualizar; si algo sale mal, se puede volver."
            : $"Copia verificada de {copied} archivos. {failed.Length} no existían y no hacía falta copiarlos.";

        Record("Copia previa a actualizar", message, backup.BackupId);
        return new UpdateReadiness(true, message, backup);
    }

    public BackupResult Rollback(string backupId)
    {
        var result = _backups.Restore(backupId);

        Record(
            result.Success ? "Datos restaurados" : "No se pudieron restaurar los datos",
            result.Detail,
            string.Empty);

        return result;
    }

    private void Record(string action, string detail, string backupId) =>
        _audit.Append(new AuditEntry
        {
            At = _clock(),
            Capability = AuditCapability.Permisos,
            Action = action,
            Detail = detail,
            Permission = "Mantenimiento de la aplicación",
            RevertHint = string.IsNullOrWhiteSpace(backupId)
                ? string.Empty
                : "Restaurar esta copia devuelve tus datos a como estaban.",
            RevertToken = backupId
        });
}
