namespace Nexo.Core.Voice;

public sealed record VoiceInputDevice(
    int DeviceNumber,
    string Name)
{
    public override string ToString() => Name;
}
