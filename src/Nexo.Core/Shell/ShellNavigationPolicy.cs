using Nexo.Core.Commands;

namespace Nexo.Core.Shell;

/// <summary>
/// Reglas de navegación del shell extraídas de <c>MainWindow</c> en la fase 1.1.
///
/// Este tipo no cambia ninguna conducta: contiene exactamente las mismas reglas que ya
/// aplicaba la ventana, expresadas como lógica pura para poder congelarlas en pruebas de
/// caracterización antes de la extracción del Runtime (ADR 0001).
///
/// La comparación de destinos es <see cref="StringComparison.OrdinalIgnoreCase"/> porque el
/// diccionario de vistas de <c>MainWindow</c> se construye con
/// <see cref="StringComparer.OrdinalIgnoreCase"/>.
/// </summary>
public static class ShellNavigationPolicy
{
    public const string Home = "Home";
    public const string Assistant = "Assistant";
    public const string Tasks = "Tasks";
    public const string Focus = "Focus";
    public const string Routines = "Routines";
    public const string Audio = "Audio";
    public const string Capture = "Capture";
    public const string System = "System";
    public const string Settings = "Settings";

    /// <summary>
    /// Destino inicial del shell al arrancar.
    ///
    /// Diseño D52 — arranca en el chat, no en un panel de resumen. Kohana es el agente con el que
    /// se habla; abrir en un tablero de tarjetas la presentaba como otra cosa, y obligaba a un clic
    /// para llegar a lo que de verdad se venía a hacer.
    /// </summary>
    public const string DefaultDestination = Assistant;

    /// <summary>
    /// Destino al que cae el shell cuando el módulo activo deja de estar visible.
    /// </summary>
    public const string FallbackDestination = Assistant;

    /// <summary>
    /// Destinos que <c>MainWindow</c> registra en su diccionario de vistas. Un destino fuera de
    /// esta lista no navega: la ventana lo ignora en silencio.
    /// </summary>
    public static IReadOnlyList<string> KnownDestinations { get; } =
    [
        Home,
        Assistant,
        Tasks,
        Focus,
        Routines,
        Audio,
        Capture,
        System,
        Settings
    ];

    /// <summary>
    /// Módulos que el usuario puede quitar del rail desde Personalización.
    ///
    /// Diseño D52 — son casi todos. Los únicos que no se pueden quitar son el chat, que es la
    /// aplicación, y Personalizar, que es desde donde se devuelven los demás: quitar ese sería
    /// cerrar la puerta desde dentro.
    /// </summary>
    public static IReadOnlyList<string> OptionalModules { get; } =
        [Home, Tasks, Focus, Routines, Audio, Capture, System];

    /// <summary>
    /// Los que llegan visibles en una instalación nueva. El resto sigue existiendo y sigue teniendo
    /// su comando «Ir a…» en la paleta; lo que no tiene es sitio fijo en el rail hasta que alguien
    /// lo pida.
    /// </summary>
    public static IReadOnlyList<string> DefaultVisibleModules { get; } = [Assistant, System, Settings];

    /// <summary>
    /// Si un módulo puede quitarse del rail. Existe como pregunta y no como lista suelta porque la
    /// interfaz de Personalización y la validación del propio rail tienen que responderla igual.
    /// </summary>
    public static bool IsOptional(string? module) =>
        module is not null &&
        OptionalModules.Any(candidate => candidate.Equals(module, StringComparison.OrdinalIgnoreCase));

    public static bool IsKnownDestination(string? destination) =>
        !string.IsNullOrWhiteSpace(destination) &&
        KnownDestinations.Contains(destination, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// El botón de Ajustes funciona como alternador: si ya estás en Ajustes vuelve al destino
    /// anterior; si no, recuerda el destino actual y entra en Ajustes.
    /// </summary>
    public static string ResolveSettingsToggle(string? currentDestination, string? previousDestination)
    {
        if (string.Equals(currentDestination, Settings, StringComparison.OrdinalIgnoreCase))
        {
            return IsKnownDestination(previousDestination)
                ? previousDestination!
                : DefaultDestination;
        }

        return Settings;
    }

    /// <summary>
    /// Destino que debe recordarse como "anterior" antes de entrar en Ajustes. Estando ya en
    /// Ajustes no se sobrescribe, para no perder el punto de retorno.
    /// </summary>
    public static string ResolvePreviousDestination(
        string? currentDestination,
        string? previousDestination)
    {
        if (string.Equals(currentDestination, Settings, StringComparison.OrdinalIgnoreCase))
        {
            return IsKnownDestination(previousDestination)
                ? previousDestination!
                : DefaultDestination;
        }

        return IsKnownDestination(currentDestination)
            ? currentDestination!
            : DefaultDestination;
    }

    /// <summary>
    /// Al ocultar un módulo opcional que además es el destino activo, el shell cae al Asistente.
    /// Ocultar un módulo que no está activo no provoca navegación.
    /// </summary>
    public static bool TryResolveHiddenModuleFallback(
        string? module,
        bool visible,
        string? currentDestination,
        out string fallbackDestination)
    {
        fallbackDestination = string.Empty;

        if (visible)
        {
            return false;
        }

        if (!string.Equals(module, currentDestination, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        fallbackDestination = FallbackDestination;
        return true;
    }

    /// <summary>
    /// Destino asociado a cada orden local de navegación. Devuelve <c>null</c> para órdenes que
    /// no son de navegación.
    /// </summary>
    public static string? ResolveNavigationCommand(LocalCommandType commandType) => commandType switch
    {
        LocalCommandType.NavigateAssistant => Assistant,
        LocalCommandType.NavigateAudio => Audio,
        LocalCommandType.NavigateCapture => Capture,
        LocalCommandType.NavigateSystem => System,
        LocalCommandType.NavigateSettings => Settings,
        _ => null
    };
}
