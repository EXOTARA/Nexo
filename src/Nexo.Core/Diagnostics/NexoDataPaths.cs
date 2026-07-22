using Nexo.Core.Branding;

namespace Nexo.Core.Diagnostics;

/// <summary>
/// Rutas privadas de Kohana. El nombre de la clase se conserva temporalmente
/// para no forzar un renombrado masivo de namespaces durante la migración.
/// </summary>
public static class NexoDataPaths
{
    private static string LocalApplicationData =>
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

    public static string RootDirectory => Path.Combine(
        LocalApplicationData,
        ProductIdentity.DataDirectoryName);

    public static string LegacyRootDirectory => Path.Combine(
        LocalApplicationData,
        ProductIdentity.LegacyDataDirectoryName);

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

    public static string VoiceCaptureLog =>
        Path.Combine(LogsDirectory, "voice-capture.log");

    public static string Settings => Path.Combine(RootDirectory, "settings.json");
    public static string Tasks => Path.Combine(RootDirectory, "tasks.json");
    public static string Focus => Path.Combine(RootDirectory, "focus.json");
    public static string Routines => Path.Combine(RootDirectory, "routines.json");
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
