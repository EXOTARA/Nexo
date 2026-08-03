using Nexo.Core.ComputerUse;
using Nexo.Core.Permissions;
using Nexo.Core.Productization;
using Nexo.Core.Settings;
using Nexo.Core.Skills;
using Nexo.Core.Vision;
using Nexo.Core.Workspace;

namespace Nexo.Core.SelfCheck;

/// <summary>
/// Diseño D22 — comprueba, **en el equipo de la persona y con el código que se está ejecutando**,
/// que las garantías que Kohana promete siguen en pie.
///
/// Por qué existe teniendo 1500 pruebas: las pruebas corren en la máquina de desarrollo, sobre el
/// código de ese momento. Esto corre aquí, ahora, sobre el binario instalado. No sustituye a las
/// pruebas ni las repite por gusto — repite justo las que, si fallaran en un equipo concreto,
/// significarían que algo que Kohana promete no se está cumpliendo en ESE equipo.
///
/// **Lo que esto NO comprueba, y hay que decirlo:** que la interfaz se vea bien, que los botones
/// hagan lo que dicen, que el dictado inserte donde toca o que la píldora no robe el foco. Eso solo
/// lo comprueba una persona usando la aplicación. Un informe verde aquí reduce el trabajo de esa
/// validación manual; no la sustituye, y presentarlo como si lo hiciera sería el peor uso posible de
/// esta clase.
/// </summary>
public static class SelfCheckSuite
{
    /// <summary>Las comprobaciones que no tocan disco. Puras, deterministas y rápidas.</summary>
    public static IReadOnlyList<SelfCheckResult> RunLogicChecks() =>
    [
        CheckMigrationLadder(),
        CheckPermissionDefaults(),
        CheckMandatoryConfirmationsCannotBeSkipped(),
        CheckExclusionsBeatPermission(),
        CheckSensitiveRedaction(),
        CheckWorkspaceContainment(),
        CheckSecretFilesAreRefused(),
        CheckSafeShellIsReadOnly(),
        CheckMethodLadderOrder(),
        CheckPacksGrantNoPermissions(),
        CheckDataInventoryIsComplete()
    ];

    private static SelfCheckResult CheckMigrationLadder()
    {
        const string id = "migracion";
        const string title = "Tus ajustes se migran sin perderse";
        const string proves =
            "Que un archivo de ajustes antiguo llega a la versión actual sin borrar lo que tuvieras.";

        // Un archivo "viejo" con valores puestos: si la migración los pisa, se ve aquí.
        var preferences = new ShellPreferences
        {
            SchemaVersion = 1,
            AccentColor = "#123456",
            WakeWordAliases = ["cojana"]
        };

        preferences.Normalize();

        if (preferences.SchemaVersion != ShellPreferences.CurrentSchemaVersion)
        {
            return SelfCheckResult.Fail(id, title, proves,
                $"La migración se quedó en la versión {preferences.SchemaVersion}.");
        }

        if (preferences.AccentColor != "#123456" || preferences.WakeWordAliases.Count == 0)
        {
            return SelfCheckResult.Fail(id, title, proves,
                "La migración borró valores que ya estaban configurados.");
        }

        return SelfCheckResult.Ok(id, title, proves,
            $"Esquema {preferences.SchemaVersion}, sin perder lo configurado.");
    }

    private static SelfCheckResult CheckPermissionDefaults()
    {
        const string id = "permisos.omision";
        const string title = "Nada peligroso viene activado de fábrica";
        const string proves =
            "Que actuar sobre el equipo llega bloqueado y el resto de permisos en «preguntar».";

        var settings = new PermissionSettings();
        settings.Normalize();

        if (settings.For(KohanaCapability.ComputerUse).Level != PermissionLevel.Bloqueado)
        {
            return SelfCheckResult.Fail(id, title, proves,
                "Actuar sobre el equipo NO está bloqueado por omisión.");
        }

        var permissive = settings.Capabilities
            .Where(entry => entry.Level == PermissionLevel.Permitido)
            .ToArray();

        return permissive.Length == 0
            ? SelfCheckResult.Ok(id, title, proves, "Todo llega en «bloqueado» o «preguntar».")
            : SelfCheckResult.Fail(id, title, proves,
                $"{permissive.Length} capacidades llegan permitidas por omisión.");
    }

    private static SelfCheckResult CheckMandatoryConfirmationsCannotBeSkipped()
    {
        const string id = "permisos.obligatorias";
        const string title = "Lo grave se pregunta siempre";
        const string proves =
            "Que borrar sin recuperación, tocar credenciales o enviar algo fuera se pregunta " +
            "aunque la capacidad esté permitida.";

        var settings = new PermissionSettings();
        settings.Normalize();
        settings.For(KohanaCapability.Memoria).Level = PermissionLevel.Permitido;

        foreach (var category in Enum.GetValues<MandatoryConfirmation>())
        {
            var decision = PermissionBroker.Decide(
                new PermissionRequest(KohanaCapability.Memoria, "prueba", null, [category]),
                settings);

            if (decision.MayProceedWithoutAsking)
            {
                return SelfCheckResult.Fail(id, title, proves,
                    $"«{MandatoryConfirmationText.Describe(category)}» pasó sin preguntar.");
            }
        }

        return SelfCheckResult.Ok(id, title, proves, "Las siete categorías preguntan siempre.");
    }

    private static SelfCheckResult CheckExclusionsBeatPermission()
    {
        const string id = "permisos.exclusiones";
        const string title = "Las aplicaciones excluidas se respetan";
        const string proves = "Que una exclusión gana incluso a una capacidad permitida.";

        var settings = new PermissionSettings();
        settings.Normalize();
        settings.For(KohanaCapability.Lens).Level = PermissionLevel.Permitido;
        settings.For(KohanaCapability.Lens).ExcludedApps = ["gestor"];

        var decision = PermissionBroker.Decide(
            new PermissionRequest(KohanaCapability.Lens, "prueba", "Mi Gestor de contraseñas"),
            settings);

        return decision.IsDenied
            ? SelfCheckResult.Ok(id, title, proves, "La exclusión bloqueó una capacidad permitida.")
            : SelfCheckResult.Fail(id, title, proves, "Una exclusión NO bloqueó una capacidad permitida.");
    }

    private static SelfCheckResult CheckSensitiveRedaction()
    {
        const string id = "redaccion";
        const string title = "Las contraseñas y claves se tapan";
        const string proves =
            "Que lo que se guarda o se envía pasa por la redacción antes de salir.";

        var samples = new[]
        {
            "contraseña: hunter2",
            "mi contraseña es hunter2",
            "token de acceso ghp_abcdefghijklmnopqrst"
        };

        var leaked = samples.Where(sample => !SensitiveContentRedactor.ContainsSensitiveContent(sample)).ToArray();
        if (leaked.Length > 0)
        {
            return SelfCheckResult.Fail(id, title, proves,
                $"{leaked.Length} ejemplos de datos sensibles NO se detectaron.");
        }

        return WorkspaceSecretScanner.ContainsSecret("ApiKey = \"sk-abcdef1234567890\"")
            ? SelfCheckResult.Ok(id, title, proves, "Datos personales y secretos de código, detectados.")
            : SelfCheckResult.Fail(id, title, proves, "Un secreto de código NO se detectó.");
    }

    private static SelfCheckResult CheckWorkspaceContainment()
    {
        const string id = "proyecto.contencion";
        const string title = "Kohana no se sale de la carpeta autorizada";
        const string proves = "Que una ruta con «..» no escapa del proyecto que autorizaste.";

        var root = Path.Combine(Path.GetTempPath(), "kohana-selfcheck", "Proyecto");
        var escape = Path.Combine(root, "..", "..", "fuera.cs");

        if (WorkspacePathPolicy.IsInside(escape, root))
        {
            return SelfCheckResult.Fail(id, title, proves, "Una ruta con «..» se consideró dentro.");
        }

        // Y la comparación por prefijo: "Proyecto" no contiene a "Proyecto-privado".
        return WorkspacePathPolicy.IsInside(root + "-privado", root)
            ? SelfCheckResult.Fail(id, title, proves, "Una carpeta hermana se consideró dentro.")
            : SelfCheckResult.Ok(id, title, proves, "Ni «..» ni carpetas hermanas entran.");
    }

    private static SelfCheckResult CheckSecretFilesAreRefused()
    {
        const string id = "proyecto.secretos";
        const string title = "Los archivos de credenciales no se leen";
        const string proves = "Que .env, claves privadas y certificados se rechazan por el nombre.";

        string[] secrets = [".env", ".env.production", "id_rsa", "servidor.pem", "certificado.pfx"];
        var readable = secrets.Where(name => !WorkspacePathPolicy.IsSecretFile(name)).ToArray();

        return readable.Length == 0
            ? SelfCheckResult.Ok(id, title, proves, "Los cinco tipos se rechazan.")
            : SelfCheckResult.Fail(id, title, proves, $"{readable.Length} tipos NO se rechazan: {string.Join(", ", readable)}.");
    }

    private static SelfCheckResult CheckSafeShellIsReadOnly()
    {
        const string id = "equipo.comandos";
        const string title = "Los comandos permitidos solo leen";
        const string proves = "Que ninguno de los comandos que Kohana puede ejecutar cambia el equipo.";

        string[] interpreters = ["powershell", "pwsh", "cmd", "wmic", "wscript", "cscript", "bash"];

        var suspicious = SafeShellCatalog.All
            .Where(command =>
                interpreters.Any(interpreter =>
                    command.Executable.Contains(interpreter, StringComparison.OrdinalIgnoreCase)) ||
                command.Arguments.Contains('>') ||
                command.Arguments.Contains("/output", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        return suspicious.Length == 0
            ? SelfCheckResult.Ok(id, title, proves, $"Los {SafeShellCatalog.All.Count} comandos solo leen.")
            : SelfCheckResult.Fail(id, title, proves,
                $"{suspicious.Length} comandos podrían cambiar algo: {string.Join(", ", suspicious.Select(command => command.Id))}.");
    }

    private static SelfCheckResult CheckMethodLadderOrder()
    {
        const string id = "equipo.metodos";
        const string title = "Se elige siempre la forma más segura";
        const string proves =
            "Que ratón y teclado simulados no se usan si hay algo mejor, y no se usan sin permiso.";

        var withBetter = ComputerUseMethodPolicy.Choose(
            [ComputerUseMethod.RatonTeclado, ComputerUseMethod.UiAutomation], simulatedInputAllowed: true);

        if (withBetter.Method != ComputerUseMethod.UiAutomation)
        {
            return SelfCheckResult.Fail(id, title, proves,
                "Se eligió un método menos seguro habiendo uno mejor disponible.");
        }

        var withoutPermission = ComputerUseMethodPolicy.Choose([ComputerUseMethod.RatonTeclado]);

        return withoutPermission.HasMethod
            ? SelfCheckResult.Fail(id, title, proves, "Ratón y teclado se usaron sin habilitarlos.")
            : SelfCheckResult.Ok(id, title, proves, "El orden se respeta y el último recurso está cerrado.");
    }

    private static SelfCheckResult CheckPacksGrantNoPermissions()
    {
        const string id = "packs.permisos";
        const string title = "Los packs no encienden permisos por su cuenta";
        const string proves = "Que activar un pack no activa la memoria, Vision ni el acceso a tu proyecto.";

        foreach (var pack in SkillPackCatalog.All)
        {
            var preferences = new ShellPreferences { VisionEnabled = false };
            preferences.Memory.Enabled = false;

            var coordinator = new SkillPackCoordinator(new InMemorySkillPackSnapshotStore(), new NullAuditLog());
            coordinator.Apply(pack, preferences);

            if (preferences.Memory.Enabled || preferences.VisionEnabled ||
                preferences.Workspace.HasAuthorizedFolder)
            {
                return SelfCheckResult.Fail(id, title, proves,
                    $"El pack {pack.Name} encendió un permiso por su cuenta.");
            }
        }

        return SelfCheckResult.Ok(id, title, proves, $"Los {SkillPackCatalog.All.Count} packs se comportan.");
    }

    private static SelfCheckResult CheckDataInventoryIsComplete()
    {
        const string id = "datos.inventario";
        const string title = "El inventario de datos está completo";
        const string proves =
            "Que cada archivo que Kohana guarda está descrito, para la copia, el borrado y el informe de privacidad.";

        var undescribed = KohanaDataInventory.All
            .Where(item => string.IsNullOrWhiteSpace(item.WhatItHolds))
            .ToArray();

        return undescribed.Length == 0
            ? SelfCheckResult.Ok(id, title, proves, $"{KohanaDataInventory.All.Count} archivos descritos.")
            : SelfCheckResult.Fail(id, title, proves, $"{undescribed.Length} archivos sin describir.");
    }

    /// <summary>Doble mínimo para poder ejercer un pack sin tocar disco.</summary>
    private sealed class InMemorySkillPackSnapshotStore : ISkillPackSnapshotStore
    {
        private SkillPackSnapshot? _snapshot;

        public SkillPackSnapshot? Load() => _snapshot?.Copy();

        public void Save(SkillPackSnapshot? snapshot) => _snapshot = snapshot?.Copy();
    }

    private sealed class NullAuditLog : Audit.IAuditLog
    {
        public IReadOnlyList<Audit.AuditEntry> Read() => [];

        public void Append(Audit.AuditEntry entry)
        {
            // La autocomprobación no debe ensuciar el registro real de la persona con sus propias
            // pruebas: lo que interesa registrar es que se hizo una comprobación, no cada paso.
        }
    }
}
