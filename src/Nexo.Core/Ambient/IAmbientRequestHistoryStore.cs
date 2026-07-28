namespace Nexo.Core.Ambient;

public interface IAmbientRequestHistoryStore
{
    AmbientRequestState Load();

    void Save(AmbientRequestState state);
}
