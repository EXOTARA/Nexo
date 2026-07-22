namespace Nexo.Core.Voice;

public enum WakeWordMatchKind
{
    None = 0,
    Exact = 1,
    Phonetic = 2,
    Approximate = 3,
    CustomAlias = 4,
    Legacy = 5
}
