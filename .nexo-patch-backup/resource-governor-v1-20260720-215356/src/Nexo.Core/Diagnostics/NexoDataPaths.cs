namespace Nexo.Core.Diagnostics;

public static class NexoDataPaths
{
    public static string RootDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Nexo");

    public static string RuntimeDirectory =>
        Path.Combine(RootDirectory, "Runtime");

    public static string OllamaRuntimeDirectory =>
        Path.Combine(RuntimeDirectory, "Ollama");

    public static string OllamaModelsDirectory =>
        Path.Combine(RootDirectory, "Models", "Ollama");

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

    public static string Settings => Path.Combine(RootDirectory, "settings.json");
    public static string Tasks => Path.Combine(RootDirectory, "tasks.json");
    public static string Focus => Path.Combine(RootDirectory, "focus.json");
    public static string Routines => Path.Combine(RootDirectory, "routines.json");
    public static string Conversation => Path.Combine(
        RootDirectory,
        "conversation-history.json");
}
