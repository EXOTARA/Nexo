using System.Text.RegularExpressions;

namespace Nexo.Core.Workspace;

/// <summary>
/// Diseño D12 (Fase 5) — detección de secretos en CÓDIGO, antes de que una sola línea salga del
/// equipo. El roadmap la nombra entre las tecnologías de la fase, y con motivo: un proyecto real
/// tiene cadenas de conexión, tokens y claves pegadas en sitios donde nadie se acuerda de que están.
///
/// Por qué no basta con <c>SensitiveContentRedactor</c>: ése busca datos PERSONALES (tarjetas,
/// contraseñas dichas en prosa) sobre texto en pantalla. Aquí lo que hay que atrapar es otra cosa —
/// asignaciones de configuración, cadenas de conexión y bloques de clave privada — con la forma que
/// tienen en un archivo de código. Se usan los dos, no uno en lugar del otro.
///
/// Redacta línea a línea y conserva el nombre de la variable: para explicar un proyecto sirve saber
/// que existe <c>ApiKey</c>; su valor no aporta nada y no debe salir de aquí.
/// </summary>
public static partial class WorkspaceSecretScanner
{
    public const string Placeholder = "[SECRETO REDACTADO]";

    public static bool ContainsSecret(string? text) =>
        !string.IsNullOrEmpty(text) && Redact(text) != text;

    public static string Redact(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text ?? string.Empty;
        }

        var result = AssignmentPattern().Replace(
            text,
            match => match.Value.Replace(match.Groups["value"].Value, Placeholder));

        result = ConnectionStringPattern().Replace(result, Placeholder);

        // Una clave privada se tapa entera, no solo su cabecera: lo sensible es el bloque.
        result = PrivateKeyBlockPattern().Replace(result, Placeholder);

        return result;
    }

    /// <summary>
    /// Asignaciones de configuración con nombre delator. Exige un valor de al menos 8 caracteres
    /// para no redactar los marcadores de posición que abundan en el código de ejemplo
    /// (<c>apiKey = ""</c>, <c>token: null</c>), que no son secretos y cuya redacción solo
    /// escondería que el hueco está vacío.
    /// </summary>
    [GeneratedRegex(
        @"(?i)\b\w*(api[_-]?key|secret|token|passwd|password|access[_-]?key|private[_-]?key|client[_-]?secret)\w*\s*[:=]\s*[""']?(?<value>[^\s""';,]{8,})[""']?",
        RegexOptions.Compiled)]
    private static partial Regex AssignmentPattern();

    /// <summary>Cadenas de conexión con credenciales incrustadas, incluidas las de URL.</summary>
    [GeneratedRegex(
        @"(?i)(?:(?:password|pwd)=[^;\s]+)|(?:[a-z][a-z0-9+.\-]*://[^\s/@:]+:[^\s/@]+@[^\s]+)",
        RegexOptions.Compiled)]
    private static partial Regex ConnectionStringPattern();

    [GeneratedRegex(
        @"-----BEGIN [A-Z ]*PRIVATE KEY-----[\s\S]*?-----END [A-Z ]*PRIVATE KEY-----",
        RegexOptions.Compiled)]
    private static partial Regex PrivateKeyBlockPattern();
}
