namespace Nexo.Core.Diagnostics;

public static class NexoDataPaths
{
    public static string RootDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Nexo");

    public static string Settings => Path.Combine(RootDirectory, "settings.json");
    public static string Tasks => Path.Combine(RootDirectory, "tasks.json");
    public static string Focus => Path.Combine(RootDirectory, "focus.json");
    public static string Routines => Path.Combine(RootDirectory, "routines.json");
    public static string Conversation => Path.Combine(RootDirectory, "conversation-history.json");
}
