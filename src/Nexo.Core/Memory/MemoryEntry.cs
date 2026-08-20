namespace Nexo.Core.Memory;

/// <summary>
/// Diseño D9 (Fase 6 — Context and Memory) — las tres categorías que decidió la sesión de roadmap.
/// Cada una se activa por separado: aceptar que Sakura recuerde tus preferencias no autoriza que
/// recuerde tus conversaciones.
/// </summary>
public enum MemoryCategory
{
    /// <summary>Preferencias declaradas ("prefiero respuestas cortas").</summary>
    Preferencias,

    /// <summary>Contexto de conversación que conviene retomar después.</summary>
    Conversacion,

    /// <summary>Hábitos de uso observados.</summary>
    Habitos
}

/// <summary>Diseño D9 — una cosa recordada. Texto plano y legible: nada opaco para el usuario.</summary>
public sealed class MemoryEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public MemoryCategory Category { get; set; }

    public string Text { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }

    public MemoryEntry Copy() => new()
    {
        Id = Id,
        Category = Category,
        Text = Text,
        CreatedAt = CreatedAt
    };
}

public sealed class MemoryState
{
    public List<MemoryEntry> Entries { get; set; } = [];

    public MemoryState Copy() => new()
    {
        Entries = Entries.Select(entry => entry.Copy()).ToList()
    };
}

public interface IMemoryStore
{
    MemoryState Load();

    void Save(MemoryState state);
}

public sealed record MemoryOperationResult(bool Success, string Message)
{
    public static MemoryOperationResult Completed(string message) => new(true, message);

    public static MemoryOperationResult Rejected(string message) => new(false, message);
}
