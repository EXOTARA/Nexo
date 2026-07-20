using System.IO;
using System.Text.Json;
using Nexo.Core.Diagnostics;

namespace Nexo.App;

public enum ShellMotionPreset
{
    Fluid,
    Snappy,
    Calm,
    None
}

public sealed class CommandPaletteState
{
    public ShellMotionPreset MotionPreset { get; set; } = ShellMotionPreset.Fluid;

    public bool ReduceMotion { get; set; }

    public List<string> RecentCommands { get; set; } = [];
}

public sealed class CommandPaletteStateStore
{
    private const int MaximumRecentCommands = 12;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _path;

    public CommandPaletteStateStore(string? path = null)
    {
        _path = string.IsNullOrWhiteSpace(path)
            ? Path.Combine(NexoDataPaths.RootDirectory, "command-palette.json")
            : path;
    }

    public CommandPaletteState Load()
    {
        try
        {
            if (!File.Exists(_path))
            {
                return new CommandPaletteState();
            }

            var json = File.ReadAllText(_path);
            var state = JsonSerializer.Deserialize<CommandPaletteState>(json, JsonOptions)
                ?? new CommandPaletteState();
            state.RecentCommands = NormalizeRecentCommands(state.RecentCommands);
            return state;
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or JsonException)
        {
            return new CommandPaletteState();
        }
    }

    public void Save(CommandPaletteState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        try
        {
            var directory = Path.GetDirectoryName(_path);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            state.RecentCommands = NormalizeRecentCommands(state.RecentCommands);
            var temporaryPath = _path + ".tmp";
            File.WriteAllText(
                temporaryPath,
                JsonSerializer.Serialize(state, JsonOptions));
            File.Move(temporaryPath, _path, overwrite: true);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
            // La paleta sigue funcionando aunque Windows no permita guardar preferencias.
        }
    }

    public void Remember(CommandPaletteState state, string command)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (string.IsNullOrWhiteSpace(command))
        {
            return;
        }

        var normalized = command.Trim();
        state.RecentCommands.RemoveAll(candidate =>
            candidate.Equals(normalized, StringComparison.OrdinalIgnoreCase));
        state.RecentCommands.Insert(0, normalized);
        state.RecentCommands = NormalizeRecentCommands(state.RecentCommands);
        Save(state);
    }

    private static List<string> NormalizeRecentCommands(IEnumerable<string>? commands)
    {
        return (commands ?? [])
            .Where(command => !string.IsNullOrWhiteSpace(command))
            .Select(command => command.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(MaximumRecentCommands)
            .ToList();
    }
}
