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
    /// </summary>
    public const string DefaultDestination = Home;

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
    /// Módulos que el usuario puede ocultar desde Personalización.
    /// </summary>
    public static IReadOnlyList<string> OptionalModules { get; } = [Audio, Capture, System];

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
