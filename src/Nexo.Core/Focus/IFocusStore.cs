namespace Nexo.Core.Focus;

public interface IFocusStore
{
    FocusState Load();

    void Save(FocusState state);
}
