using System.IO;
using System.Text.Json;
using Nexo.Core.Assistant;

namespace Nexo.Windows.Assistant;

public sealed class JsonConversationStore
{
    private const int MaxStoredMessages = 500;
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _historyPath;

    public JsonConversationStore()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        _historyPath = Path.Combine(localAppData, "Nexo", "conversation-history.json");
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
        catch (JsonException)
        {
            return Array.Empty<ConversationMessage>();
        }
        catch (IOException)
        {
            return Array.Empty<ConversationMessage>();
        }
        catch (UnauthorizedAccessException)
        {
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
            File.WriteAllText(_historyPath, json);
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
