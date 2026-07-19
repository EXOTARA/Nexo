using System.IO;
using System.Text.Json;
using Nexo.Core.Assistant;
using Nexo.Core.Diagnostics;
using Nexo.Windows.Storage;

namespace Nexo.Windows.Assistant;

public sealed class JsonConversationStore
{
    private const int MaxStoredMessages = 500;
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _historyPath;

    public JsonConversationStore(string? historyPath = null)
    {
        _historyPath = historyPath ?? NexoDataPaths.Conversation;
    }

    public IReadOnlyList<ConversationMessage> Load()
    {
        try
        {
            if (!File.Exists(_historyPath))
            {
                return Array.Empty<ConversationMessage>();
            }

            var json = File.ReadAllText(_historyPath);
            var messages = JsonSerializer.Deserialize<List<ConversationMessage>>(json);
            return messages?.TakeLast(MaxStoredMessages).ToArray() ?? [];
        }
        catch (Exception exception) when (
            exception is JsonException or IOException or UnauthorizedAccessException)
        {
            CorruptFileBackup.TryPreserve(_historyPath);
            return Array.Empty<ConversationMessage>();
        }
    }

    public void Save(IEnumerable<ConversationMessage> messages)
    {
        ArgumentNullException.ThrowIfNull(messages);

        try
        {
            var directory = Path.GetDirectoryName(_historyPath)
                ?? throw new InvalidOperationException("No se pudo determinar la carpeta del historial.");

            Directory.CreateDirectory(directory);
            var snapshot = messages.TakeLast(MaxStoredMessages).ToArray();
            var json = JsonSerializer.Serialize(snapshot, SerializerOptions);
            var temporaryPath = _historyPath + ".tmp";
            File.WriteAllText(temporaryPath, json);
            File.Move(temporaryPath, _historyPath, overwrite: true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    public void Clear()
    {
        try
        {
            if (File.Exists(_historyPath))
            {
                File.Delete(_historyPath);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
