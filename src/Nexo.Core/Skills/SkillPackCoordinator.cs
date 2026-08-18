using Nexo.Core.Audit;
using Nexo.Core.Settings;

namespace Nexo.Core.Skills;

/// <summary>Diseño D15 — un cambio concreto que el pack haría, con su antes y su después.</summary>
public sealed record SkillPackChange(string SettingId, string Title, string Current, string After);

/// <summary>
/// Diseño D15 — lo que el pack haría, ANTES de hacerlo. Trae también lo que necesita y no puede
/// encender solo: enseñar únicamente lo que gana la persona sería vender el pack, no explicarlo.
/// </summary>
public sealed record SkillPackPlan(
    SkillPack Pack,
    IReadOnlyList<SkillPackChange> Changes,
    IReadOnlyList<SkillPackRequirement> UnmetRequirements)
{
    public bool HasSomethingToDo => Changes.Count > 0;
}

/// <summary>Diseño D15 — el estado anterior a activar un pack, para poder volver a él.</summary>
public sealed class SkillPackSnapshot
{
    public string PackId { get; set; } = string.Empty;

    public DateTimeOffset AppliedAt { get; set; }

    /// <summary>Valor que tenía cada ajuste antes, por su id.</summary>
    public Dictionary<string, string> PreviousValues { get; set; } = [];

    public SkillPackSnapshot Copy() => new()
    {
        PackId = PackId,
        AppliedAt = AppliedAt,
        PreviousValues = new Dictionary<string, string>(PreviousValues)
    };
}

public interface ISkillPackSnapshotStore
{
    SkillPackSnapshot? Load();

    void Save(SkillPackSnapshot? snapshot);
}

public sealed record SkillPackResult(bool Success, string Detail)
{
    public static SkillPackResult Ok(string detail) => new(true, detail);

    public static SkillPackResult Failed(string detail) => new(false, detail);
}

/// <summary>
/// Diseño D15 (Fase 8) — activa y desactiva packs. Mismo patrón que la Fase 4 y la 5, y por el mismo
/// motivo: si algo cambia ajustes de la persona, tiene que poder deshacerse.
///
/// La regla que gobierna esta clase es la de la fase: *"cada pack hereda los permisos de las
/// capacidades que combina — no introduce excepciones"*. En consecuencia un pack **solo escribe
/// preferencias**, nunca concede un permiso. Encender la memoria, autorizar una carpeta o activar
/// Vision siguen necesitando su propia decisión; el pack se limita a decir que le harían falta y
/// dónde se activan. Un pack que encendiera permisos sería una forma de concederlos sin pedirlos,
/// disfrazada de comodidad.
///
/// Solo hay un pack activo a la vez. Dos packs superpuestos dejarían un estado que ninguno de los
/// dos describe, y "¿qué tengo activado?" dejaría de tener respuesta.
/// </summary>
public sealed class SkillPackCoordinator
{
    private readonly ISkillPackSnapshotStore _snapshots;
    private readonly IAuditLog _audit;
    private readonly Func<DateTimeOffset> _clock;

    public SkillPackCoordinator(
        ISkillPackSnapshotStore snapshots,
        IAuditLog audit,
        Func<DateTimeOffset>? clock = null)
    {
        _snapshots = snapshots;
        _audit = audit;
        _clock = clock ?? (() => DateTimeOffset.Now);
    }

    public SkillPackSnapshot? ActiveSnapshot => _snapshots.Load();

    public SkillPackId? ActivePackId =>
        Enum.TryParse<SkillPackId>(_snapshots.Load()?.PackId, out var id) ? id : null;

    /// <summary>
    /// Qué haría el pack. Los ajustes que ya están como el pack los quiere no se listan: enseñar
    /// "Dictado activado → Dictado activado" llenaría la vista previa de ruido y escondería lo que
    /// sí cambia.
    /// </summary>
    public SkillPackPlan Preview(SkillPack pack, ShellPreferences preferences)
    {
        ArgumentNullException.ThrowIfNull(pack);
        ArgumentNullException.ThrowIfNull(preferences);

        var changes = pack.Settings
            .Select(setting => new SkillPackChange(
                setting.Id,
                setting.Title,
                setting.Read(preferences),
                setting.DesiredValue))
            .Where(change => !string.Equals(change.Current, change.After, StringComparison.Ordinal))
            .ToArray();

        var unmet = pack.Requirements
            .Where(requirement => !requirement.IsSatisfied(preferences))
            .ToArray();

        return new SkillPackPlan(pack, changes, unmet);
    }

    public SkillPackResult Apply(SkillPack pack, ShellPreferences preferences)
    {
        ArgumentNullException.ThrowIfNull(pack);
        ArgumentNullException.ThrowIfNull(preferences);

        var plan = Preview(pack, preferences);
        if (!plan.HasSomethingToDo)
        {
            return SkillPackResult.Failed($"{pack.Name} ya está como debe estar: no cambié nada.");
        }

        // Con un pack ya activo, primero se vuelve al estado anterior. Si no, el snapshot del
        // segundo guardaría los ajustes del primero como si fueran los de la persona, y desactivar
        // no devolvería nada parecido a lo que tenía.
        if (_snapshots.Load() is not null)
        {
            Revert(preferences);
            plan = Preview(pack, preferences);
        }

        var snapshot = new SkillPackSnapshot
        {
            PackId = pack.Id.ToString(),
            AppliedAt = _clock()
        };

        foreach (var setting in pack.Settings)
        {
            snapshot.PreviousValues[setting.Id] = setting.Read(preferences);
        }

        // El estado anterior se guarda ANTES de tocar nada, igual que en las Fases 4 y 5.
        _snapshots.Save(snapshot);

        foreach (var setting in pack.Settings)
        {
            setting.Write(preferences, setting.DesiredValue);
        }

        // Deliberadamente NO se llama a preferences.Normalize(), por el mismo motivo ya registrado
        // en ResetVisualPreferences: Normalize arrastra la escalera de migración por SchemaVersion y
        // reasigna valores funcionales. Sobre unas preferencias que aún no hayan pasado por esa
        // escalera —las que llegan sin migrar— eso reactivaba Vision al aplicar un pack, es decir,
        // **concedía un permiso que la persona tenía apagado**: justo lo que esta clase promete no
        // hacer. Lo atrapó una prueba. Migrar no es tarea de un pack, y los valores recién escritos
        // ya son válidos: los Write de cada ajuste solo aceptan valores reconocidos.
        var detail = plan.UnmetRequirements.Count == 0
            ? $"{pack.Name} activado: {plan.Changes.Count} ajustes cambiados."
            : $"{pack.Name} activado: {plan.Changes.Count} ajustes cambiados. " +
              $"Le faltan {plan.UnmetRequirements.Count} permisos que solo puedes dar tú.";

        _audit.Append(new AuditEntry
        {
            At = _clock(),
            Capability = AuditCapability.Permisos,
            Action = $"Pack activado ({pack.Name})",
            Detail = detail,
            Permission = "Activación confirmada por el usuario",
            RevertHint = "Desactivar el pack devuelve los ajustes a como estaban.",
            RevertToken = pack.Id.ToString()
        });

        return SkillPackResult.Ok(detail);
    }

    /// <summary>Devuelve los ajustes a como estaban antes de activar el pack.</summary>
    public SkillPackResult Revert(ShellPreferences preferences)
    {
        ArgumentNullException.ThrowIfNull(preferences);

        var snapshot = _snapshots.Load();
        if (snapshot is null)
        {
            return SkillPackResult.Failed("No hay ningún pack activo que desactivar.");
        }

        if (!Enum.TryParse<SkillPackId>(snapshot.PackId, out var packId))
        {
            // Un snapshot de un pack que ya no existe no puede aplicarse a ciegas, pero tampoco debe
            // quedarse bloqueando la activación de otros: se descarta diciéndolo.
            _snapshots.Save(null);
            return SkillPackResult.Failed(
                "El pack que estaba activo ya no existe en esta versión, así que quité su marca sin " +
                "cambiar ajustes.");
        }

        var pack = SkillPackCatalog.Get(packId);
        var restored = 0;

        foreach (var setting in pack.Settings)
        {
            if (!snapshot.PreviousValues.TryGetValue(setting.Id, out var previous))
            {
                // Ajuste añadido al pack después de activarlo: no hay valor anterior que devolver, y
                // inventarse uno sería peor que dejarlo como está.
                continue;
            }

            setting.Write(preferences, previous);
            restored++;
        }

        // Tampoco aquí: deshacer un pack debe devolver EXACTAMENTE lo que la persona tenía, y
        // Normalize podría cambiar de paso otra cosa que nadie pidió tocar.
        _snapshots.Save(null);

        var detail = $"{pack.Name} desactivado: devolví {restored} ajustes a como estaban.";

        _audit.Append(new AuditEntry
        {
            At = _clock(),
            Capability = AuditCapability.Permisos,
            Action = $"Pack desactivado ({pack.Name})",
            Detail = detail,
            Permission = "Desactivación pedida por el usuario"
        });

        return SkillPackResult.Ok(detail);
    }
}
