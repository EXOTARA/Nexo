using System.Text;

namespace Nexo.Core.Ai;

public static class VisionDiagnosticPromptBuilder
{
    public const string ExtractionInstructions =
        "Eres el extractor visual técnico de Kohana. " +
        "Lee únicamente lo que realmente sea visible en la captura. " +
        "No expliques todavía la solución y no inventes código, archivos, líneas ni mensajes. " +
        "Devuelve solamente el objeto JSON solicitado. " +
        "Si no hay un error visible, marca errorVisible como false e indica exactamente qué información falta.";

    public static string BuildExtractionPrompt(string? userQuestion)
    {
        var question = string.IsNullOrWhiteSpace(userQuestion)
            ? "Analiza la captura y localiza el problema técnico visible."
            : userQuestion.Trim();

        return
            $"Pregunta del usuario: {question}\n\n" +
            "Extrae literalmente, cuando existan: tipo de problema, código de error, archivo, línea, " +
            "mensaje visible, comando ejecutado y fragmento de código relevante. " +
            "La confianza debe estar entre 0 y 1. " +
            "No supongas información que no aparezca en la imagen.";
    }

    public static string BuildResponseInstructions(VisionDiagnosticEvidence evidence)
    {
        var normalized = evidence.Normalize();
        var builder = new StringBuilder();
        builder.AppendLine("Estás en el modo de diagnóstico visual técnico de Kohana.");
        builder.AppendLine("Debes dar una respuesta accionable basada en la captura y en la evidencia estructurada.");
        builder.AppendLine();
        builder.AppendLine("Evidencia extraída:");
        builder.AppendLine($"- Error visible: {normalized.ErrorVisible}");
        builder.AppendLine($"- Tipo: {ValueOrUnknown(normalized.ProblemType)}");
        builder.AppendLine($"- Código: {ValueOrUnknown(normalized.ErrorCode)}");
        builder.AppendLine($"- Archivo: {ValueOrUnknown(normalized.FileName)}");
        builder.AppendLine($"- Línea: {(normalized.LineNumber?.ToString() ?? "no visible")}");
        builder.AppendLine($"- Mensaje: {ValueOrUnknown(normalized.VisibleMessage)}");
        builder.AppendLine($"- Comando: {ValueOrUnknown(normalized.VisibleCommand)}");
        builder.AppendLine($"- Código relevante: {ValueOrUnknown(normalized.RelevantCode)}");
        builder.AppendLine($"- Información faltante: {ValueOrUnknown(normalized.MissingInformation)}");
        builder.AppendLine($"- Confianza: {normalized.Confidence:0.00}");
        builder.AppendLine();
        builder.AppendLine("Contrato de respuesta:");
        builder.AppendLine("1. Usa los apartados: Qué ocurrió, Causa, Cómo corregirlo y Cómo comprobarlo.");
        builder.AppendLine("2. Menciona el código, archivo y línea cuando sean visibles.");
        builder.AppendLine("3. Da el cambio exacto de código o el comando concreto cuando la evidencia lo permita.");
        builder.AppendLine("4. No recomiendes contactar soporte, reinstalar o acudir a un técnico si existe un paso verificable.");
        builder.AppendLine("5. No inventes texto no visible. Si falta una línea de código, pide capturar exactamente esa zona.");
        builder.AppendLine("6. Sé directo y evita explicaciones genéricas.");

        if (!normalized.ErrorVisible)
        {
            builder.AppendLine("7. Como no se detectó un error legible, dilo claramente y solicita una captura más útil sin inventar un diagnóstico.");
        }

        return builder.ToString().Trim();
    }

    public static string BuildEvidenceBanner(VisionDiagnosticEvidence evidence)
    {
        var normalized = evidence.Normalize();
        if (!normalized.ErrorVisible)
        {
            return "Kohana detectó: no hay un error legible en la captura.";
        }

        return $"Kohana detectó: {normalized.BuildCompactSummary()}";
    }

    private static string ValueOrUnknown(string value) =>
        string.IsNullOrWhiteSpace(value) ? "no visible" : value;
}
