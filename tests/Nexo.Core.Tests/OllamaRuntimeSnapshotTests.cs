using Nexo.Core.Ai;

namespace Nexo.Core.Tests;

public sealed class OllamaRuntimeSnapshotTests
{
    [Fact]
    public void Unavailable_IsNotRunningOrManaged()
    {
        var snapshot = new OllamaRuntimeSnapshot(
            OllamaRuntimeState.Unavailable,
            "http://localhost:11434/v1",
            null,
            "Ollama no está disponible.");

        Assert.False(snapshot.IsRunning);
        Assert.False(snapshot.IsManaged);
        Assert.False(snapshot.CanStartManaged);
    }

    [Fact]
    public void ManagedInstalled_CanBeStarted()
    {
        var snapshot = new OllamaRuntimeSnapshot(
            OllamaRuntimeState.ManagedInstalled,
            "http://localhost:11434/v1",
            @"C:\Nexo\Runtime\Ollama\ollama.exe",
            "La copia administrada está instalada.");

        Assert.False(snapshot.IsRunning);
        Assert.True(snapshot.IsManaged);
        Assert.True(snapshot.CanStartManaged);
    }

    [Theory]
    [InlineData(OllamaRuntimeState.ExternalRunning, false)]
    [InlineData(OllamaRuntimeState.ManagedRunning, true)]
    public void RunningStates_AreReportedCorrectly(
        OllamaRuntimeState state,
        bool expectedManaged)
    {
        var snapshot = new OllamaRuntimeSnapshot(
            state,
            "http://localhost:11434/v1",
            expectedManaged ? @"C:\Nexo\Runtime\Ollama\ollama.exe" : null,
            "Ollama está funcionando.");

        Assert.True(snapshot.IsRunning);
        Assert.Equal(expectedManaged, snapshot.IsManaged);
        Assert.False(snapshot.CanStartManaged);
    }
}