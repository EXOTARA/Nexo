using System.Text;

namespace Nexo.Core.Diagnostics;

public sealed record NexoDiagnosticSnapshot(
    DateTimeOffset CapturedAt,
    string AppVersion,
    string OperatingSystem,
    string RuntimeVersion,
    string DataDirectory,
    IReadOnlyList<DiagnosticItem> Items)
{
    public string ToClipboardText()
    {
        var builder = new StringBuilder();
        builder.AppendLine("Nexo · diagnóstico local");
        builder.AppendLine($"Capturado: {CapturedAt:O}");
        builder.AppendLine($"Versión: {AppVersion}");
        builder.AppendLine($"Sistema: {OperatingSystem}");
        builder.AppendLine($"Runtime: {RuntimeVersion}");
        builder.AppendLine($"Datos: {DataDirectory}");
        builder.AppendLine();

        foreach (var item in Items)
        {
            builder.AppendLine($"[{item.Status}] {item.Name}: {item.Detail}");
        }

        return builder.ToString().TrimEnd();
    }
}
