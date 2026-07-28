namespace Nexo.Core.Ambient;

public sealed class AmbientRequestState
{
    public AmbientRequest? ActiveRequest { get; set; }

    public List<AmbientRequestHistoryEntry> History { get; set; } = [];

    public AmbientRequestState Copy() => new()
    {
        ActiveRequest = ActiveRequest?.Copy(),
        History = History.Select(entry => entry.Copy()).ToList()
    };
}
