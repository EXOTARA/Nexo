using Nexo.Core.AdaptiveEngine;
using Nexo.Core.Flow;
using Nexo.Core.Settings;

namespace Nexo.Core.Skills;

/// <summary>
/// Diseño D15 (Fase 8) — los dos packs. Cada ajuste que tocan corresponde a una capacidad que ya
/// existe y está probada; cada cosa que necesitan y no pueden encender por su cuenta está declarada
/// como requisito, no aplicada en silencio.
///
/// El riesgo que el roadmap señala para esta fase es la fragmentación: *"si cada pack termina con su
/// propia lógica en vez de reutilizar capacidades comunes"*. Por eso un pack aquí no es código, es
/// una LISTA: no puede hacer nada que Personalizar no pueda hacer ya, solo lo deja puesto de una vez.
/// </summary>
public static class SkillPackCatalog
{
    public static IReadOnlyList<SkillPack> All => [Study, Dev, Support, Creator, Access, Meeting];

    public static SkillPack Get(SkillPackId id) => id switch
    {
        SkillPackId.Study => Study,
        SkillPackId.Dev => Dev,
        SkillPackId.Support => Support,
        SkillPackId.Creator => Creator,
        SkillPackId.Access => Access,
        SkillPackId.Meeting => Meeting,
        _ => throw new ArgumentOutOfRangeException(nameof(id), id, "Ese pack no existe.")
    };

    /// <summary>
    /// Kohana Study — para leer, estudiar y tomar apuntes. Dicta en prosa, contesta en voz alta y
    /// quita de en medio lo que distrae.
    /// </summary>
    public static SkillPack Study { get; } = new(
        SkillPackId.Study,
        "Kohana Study",
        "Para estudiar: dictar apuntes, oír las respuestas y tener menos cosas parpadeando alrededor.",
        [
            Boolean("flow.enabled", "Dictado global activado",
                preferences => preferences.FlowEnabled,
                (preferences, value) => preferences.FlowEnabled = value,
                desired: true),

            Enum("flow.mode", "Estilo de dictado en Texto",
                preferences => preferences.FlowMode,
                (preferences, value) => preferences.FlowMode = value,
                desired: FlowMode.Texto),

            Boolean("voice.speak", "Kohana lee sus respuestas en voz alta",
                preferences => preferences.SpeakVoiceResponses,
                (preferences, value) => preferences.SpeakVoiceResponses = value,
                desired: true),

            Boolean("peek.enabled", "Vista rápida apagada mientras estudias",
                preferences => preferences.PeekEnabled,
                (preferences, value) => preferences.PeekEnabled = value,
                desired: false),

            Enum("hardware.mode", "Rendimiento equilibrado",
                preferences => preferences.HardwarePerformanceMode,
                (preferences, value) => preferences.HardwarePerformanceMode = value,
                desired: HardwarePerformanceMode.Balanced)
        ],
        [
            new SkillPackRequirement(
                "Que Kohana pueda ver la pantalla (Lens)",
                "Sin esto no puede explicarte lo que estás leyendo, solo lo que le dictes.",
                preferences => preferences.VisionEnabled,
                "Actívalo en Personalizar → Vision."),

            new SkillPackRequirement(
                "Memoria personal",
                "Sin memoria, cada sesión de estudio empieza de cero y hay que repetirle el contexto.",
                preferences => preferences.Memory.Enabled,
                "Actívala en Personalizar → Memoria personal."),

            new SkillPackRequirement(
                "Un proveedor de IA configurado",
                "Las explicaciones abiertas las responde el modelo, no una lista de comandos.",
                preferences => preferences.AiProvider != Ai.AiProviderKind.Disabled,
                "Elige uno en Personalizar → Inteligencia artificial.")
        ]);

    /// <summary>
    /// Kohana Dev — para programar. Dicta con símbolos, deja Sistema y Captura a mano y le pide a
    /// Kohana toda la potencia.
    /// </summary>
    public static SkillPack Dev { get; } = new(
        SkillPackId.Dev,
        "Kohana Dev",
        "Para programar: dictado con símbolos, Sistema y Captura a la vista, y Kohana a pleno rendimiento.",
        [
            Boolean("flow.enabled", "Dictado global activado",
                preferences => preferences.FlowEnabled,
                (preferences, value) => preferences.FlowEnabled = value,
                desired: true),

            Enum("flow.mode", "Estilo de dictado en Código",
                preferences => preferences.FlowMode,
                (preferences, value) => preferences.FlowMode = value,
                desired: FlowMode.Codigo),

            Boolean("modules.system", "Módulo Sistema visible",
                preferences => preferences.ShowSystemModule,
                (preferences, value) => preferences.ShowSystemModule = value,
                desired: true),

            Boolean("modules.capture", "Módulo Captura visible",
                preferences => preferences.ShowCaptureModule,
                (preferences, value) => preferences.ShowCaptureModule = value,
                desired: true),

            Enum("hardware.mode", "Rendimiento al máximo",
                preferences => preferences.HardwarePerformanceMode,
                (preferences, value) => preferences.HardwarePerformanceMode = value,
                desired: HardwarePerformanceMode.Maximum)
        ],
        [
            new SkillPackRequirement(
                "Una carpeta de proyecto autorizada",
                "Es lo que le permite leer tu código y explicártelo. El pack no la autoriza por ti.",
                preferences => preferences.Workspace.HasAuthorizedFolder,
                "Autorízala en Personalizar → Proyecto autorizado."),

            new SkillPackRequirement(
                "Que Kohana pueda ver la pantalla (Lens)",
                "Sin esto no puede leer el error que tienes delante.",
                preferences => preferences.VisionEnabled,
                "Actívalo en Personalizar → Vision."),

            new SkillPackRequirement(
                "Un proveedor de IA configurado",
                "Explicar código y proponer cambios lo hace el modelo.",
                preferences => preferences.AiProvider != Ai.AiProviderKind.Disabled,
                "Elige uno en Personalizar → Inteligencia artificial.")
        ]);

    /// <summary>
    /// Kohana Support — para arreglar algo que no funciona. Deja Sistema y Captura a mano, con las
    /// métricas visibles en la vista rápida.
    /// </summary>
    public static SkillPack Support { get; } = new(
        SkillPackId.Support,
        "Kohana Support",
        "Para resolver un problema: Sistema y Captura a la vista, métricas delante y dictado normal.",
        [
            Boolean("modules.system", "Módulo Sistema visible",
                preferences => preferences.ShowSystemModule,
                (preferences, value) => preferences.ShowSystemModule = value,
                desired: true),

            Boolean("modules.capture", "Módulo Captura visible",
                preferences => preferences.ShowCaptureModule,
                (preferences, value) => preferences.ShowCaptureModule = value,
                desired: true),

            Boolean("peek.enabled", "Vista rápida activada",
                preferences => preferences.PeekEnabled,
                (preferences, value) => preferences.PeekEnabled = value,
                desired: true),

            Boolean("peek.topProcess", "Ver qué programa consume más",
                preferences => preferences.ShowTopProcessInPeek,
                (preferences, value) => preferences.ShowTopProcessInPeek = value,
                desired: true),

            Enum("flow.mode", "Estilo de dictado en Texto",
                preferences => preferences.FlowMode,
                (preferences, value) => preferences.FlowMode = value,
                desired: FlowMode.Texto)
        ],
        [
            new SkillPackRequirement(
                "Que Kohana pueda ver la pantalla (Lens)",
                "Es lo que le deja leer el error que tienes delante en vez de que se lo describas.",
                preferences => preferences.VisionEnabled,
                "Actívalo en Personalizar → Vision."),

            new SkillPackRequirement(
                "Compartir métricas del sistema con la IA",
                "Sin esto puede ver la pantalla, pero no relacionar el problema con el uso de CPU o memoria.",
                preferences => preferences.ShareSystemMetricsWithAi,
                "Actívalo en Personalizar → Inteligencia artificial.")
        ]);

    /// <summary>
    /// Kohana Creator — para editar, diseñar o montar algo. Todo el rendimiento y nada parpadeando
    /// alrededor.
    /// </summary>
    public static SkillPack Creator { get; } = new(
        SkillPackId.Creator,
        "Kohana Creator",
        "Para crear sin interrupciones: máximo rendimiento, sin avisos y con el audio por aplicación a mano.",
        [
            Enum("hardware.mode", "Rendimiento al máximo",
                preferences => preferences.HardwarePerformanceMode,
                (preferences, value) => preferences.HardwarePerformanceMode = value,
                desired: HardwarePerformanceMode.Maximum),

            Boolean("modules.audio", "Módulo Audio visible",
                preferences => preferences.ShowAudioModule,
                (preferences, value) => preferences.ShowAudioModule = value,
                desired: true),

            Boolean("peek.enabled", "Vista rápida apagada",
                preferences => preferences.PeekEnabled,
                (preferences, value) => preferences.PeekEnabled = value,
                desired: false),

            Boolean("notifications.windows", "Sin avisos de Windows",
                preferences => preferences.ShowWindowsNotifications,
                (preferences, value) => preferences.ShowWindowsNotifications = value,
                desired: false),

            Boolean("notifications.sounds", "Sin sonidos de aviso",
                preferences => preferences.PlayNotificationSounds,
                (preferences, value) => preferences.PlayNotificationSounds = value,
                desired: false)
        ],
        [
            new SkillPackRequirement(
                "Un proveedor de IA configurado",
                "Las ideas y los textos los redacta el modelo, no una lista de comandos.",
                preferences => preferences.AiProvider != Ai.AiProviderKind.Disabled,
                "Elige uno en Personalizar → Inteligencia artificial.")
        ]);

    /// <summary>
    /// Kohana Access — manos libres y menos movimiento en pantalla. Es el pack que más se nota en el
    /// día a día de quien lo necesita, y el que menos capacidades nuevas usa: todo esto ya existía.
    /// </summary>
    public static SkillPack Access { get; } = new(
        SkillPackId.Access,
        "Kohana Access",
        "Para usar Kohana sin manos y con menos movimiento: responde en voz alta, escucha siempre y no anima nada.",
        [
            Boolean("voice.speak", "Kohana lee sus respuestas en voz alta",
                preferences => preferences.SpeakVoiceResponses,
                (preferences, value) => preferences.SpeakVoiceResponses = value,
                desired: true),

            Boolean("wakeword.enabled", "Responde a la palabra de activación",
                preferences => preferences.WakeWordEnabled,
                (preferences, value) => preferences.WakeWordEnabled = value,
                desired: true),

            Boolean("shell.animations", "Sin animaciones",
                preferences => preferences.AnimationsEnabled,
                (preferences, value) => preferences.AnimationsEnabled = value,
                desired: false),

            Boolean("flow.enabled", "Dictado global activado",
                preferences => preferences.FlowEnabled,
                (preferences, value) => preferences.FlowEnabled = value,
                desired: true)
        ],
        [
            new SkillPackRequirement(
                "Micrófono y modelos de voz preparados",
                "La palabra de activación y el dictado necesitan que Whisper y Vosk estén descargados.",
                preferences => preferences.VoiceInputDeviceNumber >= -1,
                "Compruébalo en Sistema → Runtime, o con «Comprobar que Kohana funciona bien»."),

            new SkillPackRequirement(
                "Un proveedor de IA configurado",
                "Sin IA responde a comandos conocidos, pero no a preguntas abiertas.",
                preferences => preferences.AiProvider != Ai.AiProviderKind.Disabled,
                "Elige uno en Personalizar → Inteligencia artificial.")
        ]);

    /// <summary>
    /// Kohana Meeting — durante una videollamada. Kohana se aparta: ni avisos, ni voz, ni competir
    /// por el procesador con la llamada.
    /// </summary>
    public static SkillPack Meeting { get; } = new(
        SkillPackId.Meeting,
        "Kohana Meeting",
        "Para una reunión: Kohana se calla, no interrumpe y baja su consumo para no competir con la llamada.",
        [
            Boolean("voice.speak", "Kohana no habla en voz alta",
                preferences => preferences.SpeakVoiceResponses,
                (preferences, value) => preferences.SpeakVoiceResponses = value,
                desired: false),

            Boolean("wakeword.enabled", "No escucha la palabra de activación",
                preferences => preferences.WakeWordEnabled,
                (preferences, value) => preferences.WakeWordEnabled = value,
                desired: false),

            Boolean("notifications.windows", "Sin avisos de Windows",
                preferences => preferences.ShowWindowsNotifications,
                (preferences, value) => preferences.ShowWindowsNotifications = value,
                desired: false),

            Boolean("notifications.sounds", "Sin sonidos de aviso",
                preferences => preferences.PlayNotificationSounds,
                (preferences, value) => preferences.PlayNotificationSounds = value,
                desired: false),

            Enum("hardware.mode", "Kohana en bajo consumo",
                preferences => preferences.HardwarePerformanceMode,
                (preferences, value) => preferences.HardwarePerformanceMode = value,
                desired: HardwarePerformanceMode.Eco)
        ],
        [
            // Meeting no necesita permisos nuevos, y decirlo es parte de explicarlo: es el único
            // pack que solo apaga cosas.
            new SkillPackRequirement(
                "Nada más: este pack solo apaga cosas",
                "No pide ningún permiso nuevo, así que puedes activarlo sin conceder nada.",
                _ => true,
                "No hay que hacer nada.")
        ]);

    private static SkillPackSetting Boolean(
        string id,
        string title,
        Func<ShellPreferences, bool> read,
        Action<ShellPreferences, bool> write,
        bool desired) =>
        new(
            id,
            title,
            preferences => read(preferences) ? "Sí" : "No",
            (preferences, value) => write(preferences, string.Equals(value, "Sí", StringComparison.OrdinalIgnoreCase)),
            desired ? "Sí" : "No");

    private static SkillPackSetting Enum<TValue>(
        string id,
        string title,
        Func<ShellPreferences, TValue> read,
        Action<ShellPreferences, TValue> write,
        TValue desired)
        where TValue : struct, Enum =>
        new(
            id,
            title,
            preferences => read(preferences).ToString()!,
            (preferences, value) =>
            {
                // Un valor que no se reconoce se ignora en vez de romper: al deshacer un pack, el
                // archivo de estado puede venir de una versión anterior con otros nombres.
                if (System.Enum.TryParse<TValue>(value, out var parsed) && System.Enum.IsDefined(parsed))
                {
                    write(preferences, parsed);
                }
            },
            desired.ToString()!);
}
