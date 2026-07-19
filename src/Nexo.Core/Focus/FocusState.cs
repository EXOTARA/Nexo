namespace Nexo.Core.Focus;

public sealed class FocusState
{
    public FocusTimer? ActiveTimer { get; set; }

    public List<FocusHistoryEntry> History { get; set; } = [];

    public FocusState Copy() => new()
    {
        ActiveTimer = ActiveTimer?.Copy(),
        History = History.Select(entry => entry.Copy()).ToList()
    };
}
