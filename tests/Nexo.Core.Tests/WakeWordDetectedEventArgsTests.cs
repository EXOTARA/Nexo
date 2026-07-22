using Nexo.Core.Voice;

namespace Nexo.Core.Tests;

public sealed class WakeWordDetectedEventArgsTests
{
    [Fact]
    public void Constructor_CopiesPreRollAudio()
    {
        var source = new byte[] { 1, 2, 3, 4 };
        var args = new WakeWordDetectedEventArgs(
            WakeWordPhrase.Nexo,
            "nexo",
            source);

        source[0] = 99;

        Assert.Equal(new byte[] { 1, 2, 3, 4 }, args.PreRollAudio.ToArray());
    }

    [Fact]
    public void Constructor_CopiesPostWakeAudio()
    {
        var source = new byte[] { 5, 6, 7 };
        var args = new WakeWordDetectedEventArgs(
            WakeWordPhrase.HeyNexo,
            "hey nexo",
            postWakeAudio: source);

        source[0] = 99;

        Assert.Equal(new byte[] { 5, 6, 7 }, args.PostWakeAudio.ToArray());
    }

    [Fact]
    public void Constructor_AllowsEmptyPreRoll()
    {
        var args = new WakeWordDetectedEventArgs(
            WakeWordPhrase.OyeNexo,
            "oye nexo");

        Assert.True(args.PreRollAudio.IsEmpty);
        Assert.True(args.PostWakeAudio.IsEmpty);
    }

    [Fact]
    public void Constructor_PreservesMatchKind()
    {
        var args = new WakeWordDetectedEventArgs(
            WakeWordPhrase.OyeKohana,
            "oye cojana",
            matchKind: WakeWordMatchKind.Phonetic);

        Assert.Equal(WakeWordMatchKind.Phonetic, args.MatchKind);
    }
}
