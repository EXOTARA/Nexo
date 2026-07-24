using Nexo.Core.Diagnostics;

namespace Nexo.Core.Tests;

public sealed class NexoDiagnosticSnapshotTests
{
    [Fact]
    public void ToClipboardText_IncludesTechnicalStateWithoutExtraContent()
    {
        var snapshot = new NexoDiagnosticSnapshot(
            new DateTimeOffset(2026, 7, 19, 10, 30, 0, TimeSpan.Zero),
            "1.0.0",
            "Windows",
            ".NET 10",
            @"C:\Users\Test\AppData\Local\Kohana",
            [
                new DiagnosticItem("Ollama", DiagnosticStatus.Ready, "Conectado."),
                new DiagnosticItem("Whisper", DiagnosticStatus.Warning, "Pendiente.")
            ]);

        var text = snapshot.ToClipboardText();

        Assert.Contains("Kohana · diagnóstico local", text);
        Assert.Contains("[Ready] Ollama: Conectado.", text);
        Assert.Contains("[Warning] Whisper: Pendiente.", text);
        Assert.False(text.Contains("conversation", StringComparison.OrdinalIgnoreCase));
    }
}
