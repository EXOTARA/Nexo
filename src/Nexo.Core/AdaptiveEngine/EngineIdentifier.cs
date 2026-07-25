namespace Nexo.Core.AdaptiveEngine;

public readonly record struct EngineIdentifier(string Value)
{
    public override string ToString() => Value;
}
