using Nexo.Core.Branding;

namespace Nexo.Core.Diagnostics;

/// <summary>
/// Rutas privadas de Kohana. El nombre de la clase se conserva temporalmente
/// para no forzar un renombrado masivo de namespaces durante la migración.
///
/// Diseño D3.2 — punto único de resolución de la raíz de datos. Antes de este diseño, todas las
/// propiedades ya derivaban de <see cref="RootDirectory"/> (ningún store leía su propia variable
/// de entorno por separado); este diseño solo le enseña a <see cref="RootDirectory"/> a resolver
/// un override — nada más en esta clase cambia de forma. Orden de precedencia:
/// 1. <see cref="UseExplicitDataRoot"/> (fijado por el proceso, p. ej. un futuro flag de línea de
///    comandos o un arnés de pruebas/validación).
/// 2. Variable de entorno <see cref="DataRootEnvironmentVariable"/> (pensada para scripts de
///    validación y desarrollo).
/// 3. Ruta de producción real (`%LocalAppData%\Kohana`) — sin cambios de comportamiento cuando no
///    hay ningún override activo.
///
/// Cuando hay un override activo, <see cref="LegacyRootDirectory"/> colapsa a la misma raíz que
/// <see cref="RootDirectory"/>: esto hace que <c>LegacyDataMigrator</c> vea origen == destino y no
/// copie nada — un perfil de validación nunca debe leer ni migrar los datos reales del usuario
/// (`%LocalAppData%\Nexo`), ni mezclarse con el perfil normal.
/// </summary>
public static class NexoDataPaths
{
    /// <summary>
    /// Diseño D3.2 — variable de entorno para apuntar la raíz de datos a un perfil aislado
    /// (validación interactiva, desarrollo, automatización, diagnóstico). No documentada como
    /// opción de usuario final: no hay selector en la UI normal.
    /// </summary>
    public const string DataRootEnvironmentVariable = "KOHANA_DATA_ROOT";

    private static string? _explicitOverrideRoot;

    private static string LocalApplicationData =>
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

    /// <summary>
    /// Diseño D3.2 — fija (o limpia, con <c>null</c>/cadena vacía) una raíz de datos explícita,
    /// con prioridad sobre la variable de entorno. Nunca se activa sola: alguien tiene que
    /// llamarla a propósito, así que el comportamiento de producción no cambia por defecto.
    /// Una cadena vacía o solo espacios NUNCA se acepta como una raíz real — se trata igual que
    /// "sin override", no como "usar la carpeta actual" ni ninguna otra ruta implícita.
    /// </summary>
    public static void UseExplicitDataRoot(string? path)
    {
        _explicitOverrideRoot = NormalizeOverride(path);
    }

    /// <summary>Diseño D3.2 — true si hay un perfil de validación/desarrollo activo.</summary>
    public static bool IsUsingOverrideRoot => ResolveOverrideRoot() is not null;

    /// <summary>
    /// Diseño D3.2 — la raíz de datos activa junto con de dónde vino, pensado para dejar
    /// constancia en un log de diagnóstico sin arriesgar exponer la ruta a quien no la pidió.
    /// </summary>
    public static (string Root, string Source) DescribeActiveRoot()
    {
        if (_explicitOverrideRoot is not null)
        {
            return (_explicitOverrideRoot, "explícito (proceso)");
        }

        var envOverride = NormalizeOverride(
            Environment.GetEnvironmentVariable(DataRootEnvironmentVariable));
        return envOverride is not null
            ? (envOverride, $"variable de entorno {DataRootEnvironmentVariable}")
            : (RootDirectory, "producción");
    }

    public static string RootDirectory =>
        ResolveOverrideRoot() ??
        Path.Combine(LocalApplicationData, ProductIdentity.DataDirectoryName);

    public static string LegacyRootDirectory =>
        IsUsingOverrideRoot
            ? RootDirectory
            : Path.Combine(LocalApplicationData, ProductIdentity.LegacyDataDirectoryName);

    private static string? ResolveOverrideRoot()
    {
        if (_explicitOverrideRoot is not null)
        {
            return _explicitOverrideRoot;
        }

        return NormalizeOverride(Environment.GetEnvironmentVariable(DataRootEnvironmentVariable));
    }

    /// <summary>
    /// Diseño D3.2 — una ruta inválida (vacía, solo espacios, o que .NET no pueda resolver a una
    /// ruta absoluta) nunca se acepta en silencio como si fuera válida: se normaliza a "sin
    /// override", que cae de vuelta a la siguiente prioridad (variable de entorno o producción),
    /// en vez de lanzar y tumbar el arranque de la aplicación por un valor mal escrito.
    /// </summary>
    private static string? NormalizeOverride(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        try
        {
            return Path.GetFullPath(path.Trim());
        }
        catch (Exception exception) when (
            exception is ArgumentException or PathTooLongException or NotSupportedException)
        {
            return null;
        }
    }

    public static string RuntimeDirectory => Path.Combine(
        RootDirectory,
        "Runtime");

    public static string LegacyRuntimeDirectory => Path.Combine(
        LegacyRootDirectory,
        "Runtime");

    public static string OllamaRuntimeDirectory => PreferExistingDirectory(
        Path.Combine(RuntimeDirectory, "Ollama"),
        Path.Combine(LegacyRuntimeDirectory, "Ollama"));

    public static string ModelsDirectory => Path.Combine(
        RootDirectory,
        "Models");

    public static string LegacyModelsDirectory => Path.Combine(
        LegacyRootDirectory,
        "Models");

    public static string OllamaModelsDirectory => PreferExistingDirectory(
        Path.Combine(ModelsDirectory, "Ollama"),
        Path.Combine(LegacyModelsDirectory, "Ollama"));

    public static string VoskModelsDirectory => PreferExistingDirectory(
        Path.Combine(ModelsDirectory, "Vosk"),
        Path.Combine(LegacyModelsDirectory, "Vosk"));

    public static string WhisperModel => PreferExistingFile(
        Path.Combine(ModelsDirectory, "ggml-base.bin"),
        Path.Combine(LegacyModelsDirectory, "ggml-base.bin"));

    public static string OllamaExecutable =>
        Path.Combine(OllamaRuntimeDirectory, "ollama.exe");

    public static string TempDirectory =>
        Path.Combine(RootDirectory, "Temp");

    public static string OllamaInstallerTempDirectory =>
        Path.Combine(TempDirectory, "OllamaInstaller");

    public static string LogsDirectory =>
        Path.Combine(RootDirectory, "Logs");

    public static string OllamaRuntimeLog =>
        Path.Combine(LogsDirectory, "ollama-runtime.log");

    public static string ResourceGovernorLog =>
        Path.Combine(LogsDirectory, "resource-governor.log");

    /// <summary>
    /// Fallos de comandos del Sakura Command Center (Diseño D2). Guarda el detalle técnico que la
    /// notificación no modal no muestra: tipo, mensaje, excepción interna y stack trace.
    /// </summary>
    public static string CommandCenterLog =>
        Path.Combine(LogsDirectory, "command-center.log");

    public static string VoiceCaptureLog =>
        Path.Combine(LogsDirectory, "voice-capture.log");

    public static string WakeWordRecognitionLog =>
        Path.Combine(LogsDirectory, "wake-word-recognition.log");

    public static string Settings => Path.Combine(RootDirectory, "settings.json");
    public static string Tasks => Path.Combine(RootDirectory, "tasks.json");
    public static string Focus => Path.Combine(RootDirectory, "focus.json");
    public static string Routines => Path.Combine(RootDirectory, "routines.json");
    public static string AmbientRequests => Path.Combine(RootDirectory, "ambient-requests.json");
    public static string Conversation => Path.Combine(
        RootDirectory,
        "conversation-history.json");

    private static string PreferExistingDirectory(
        string currentPath,
        string legacyPath) =>
        Directory.Exists(currentPath) || !Directory.Exists(legacyPath)
            ? currentPath
            : legacyPath;

    private static string PreferExistingFile(
        string currentPath,
        string legacyPath) =>
        File.Exists(currentPath) || !File.Exists(legacyPath)
            ? currentPath
            : legacyPath;
}
