namespace Nexo.Core.Workspace;

/// <summary>
/// Diseño D14 (Fase 5, nivel 4) — el estado de UN archivo antes de que Kohana lo tocara. El modelo
/// de confianza no lo deja a elección: *"toda acción de las Fases 4, 5 y 7 que modifique estado
/// persistente debe generar un snapshot previo reversible antes de ejecutarse. Sin snapshot previo,
/// la acción no debe ofrecerse en niveles de autonomía 4 o superiores."*
///
/// Guarda también lo que Kohana ESCRIBIÓ, no solo lo que había antes. Suena redundante y es la pieza
/// que evita el peor daño posible de esta capacidad: si la persona editó el archivo después, deshacer
/// borraría su trabajo. Comparando con lo escrito se detecta ese caso y se rehúsa.
/// </summary>
public sealed class WorkspaceCheckpoint
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public DateTimeOffset At { get; set; }

    public string RelativePath { get; set; } = string.Empty;

    /// <summary>Contenido anterior. Vacío cuando el archivo no existía.</summary>
    public string PreviousContent { get; set; } = string.Empty;

    /// <summary>
    /// Si el archivo existía antes. Deshacer un archivo que Kohana creó significa borrarlo; deshacer
    /// uno que ya estaba significa devolverle su contenido. No es la misma operación.
    /// </summary>
    public bool PreviousExisted { get; set; }

    /// <summary>Lo que Kohana dejó escrito, para saber si alguien lo cambió después.</summary>
    public string WrittenContent { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public WorkspaceCheckpoint Copy() => new()
    {
        Id = Id,
        At = At,
        RelativePath = RelativePath,
        PreviousContent = PreviousContent,
        PreviousExisted = PreviousExisted,
        WrittenContent = WrittenContent,
        Description = Description
    };
}

public sealed class WorkspaceCheckpointState
{
    /// <summary>
    /// Tope de checkpoints guardados. Cada uno contiene el archivo entero, así que sin límite el
    /// historial acabaría pesando más que el propio proyecto.
    /// </summary>
    public const int MaximumCheckpoints = 25;

    public List<WorkspaceCheckpoint> Checkpoints { get; set; } = [];

    public WorkspaceCheckpointState Copy() => new()
    {
        Checkpoints = Checkpoints.Select(checkpoint => checkpoint.Copy()).ToList()
    };
}

public interface IWorkspaceCheckpointStore
{
    IReadOnlyList<WorkspaceCheckpoint> Read();

    void Save(WorkspaceCheckpoint checkpoint);

    void Remove(Guid checkpointId);
}
