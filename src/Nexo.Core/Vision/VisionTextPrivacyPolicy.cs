using System.Text.RegularExpressions;

namespace Nexo.Core.Vision;

public static partial class VisionTextPrivacyPolicy
{
    public static IReadOnlyList<VisionTextPrivacyFinding> Analyze(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        var findings = new List<VisionTextPrivacyFinding>();
        AddFinding(findings, "email", "Direcciones de correo", EmailRegex().Matches(text).Count);
        AddFinding(findings, "api-key", "Posibles claves o tokens", SecretRegex().Matches(text).Count);
        AddFinding(findings, "credential", "Palabras relacionadas con credenciales", CredentialRegex().Matches(text).Count);
        return findings;
    }

    public static IReadOnlyList<string> ParseCustomExclusions(string? value) =>
        (value ?? string.Empty)
            .Split([',', ';', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(item => item.Length >= 2)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private static void AddFinding(
        ICollection<VisionTextPrivacyFinding> findings,
        string kind,
        string description,
        int count)
    {
        if (count > 0)
        {
            findings.Add(new VisionTextPrivacyFinding(kind, description, count));
        }
    }

    [GeneratedRegex(@"\b[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,}\b", RegexOptions.IgnoreCase)]
    private static partial Regex EmailRegex();

    [GeneratedRegex(@"\b(?:sk-[A-Za-z0-9_-]{12,}|gh[pousr]_[A-Za-z0-9_]{12,}|AIza[0-9A-Za-z_-]{20,}|Bearer\s+[A-Za-z0-9._~+/=-]{12,})\b", RegexOptions.IgnoreCase)]
    private static partial Regex SecretRegex();

    [GeneratedRegex(@"\b(?:password|contraseña|passwd|api[_ -]?key|access[_ -]?token|secret|token)\b", RegexOptions.IgnoreCase)]
    private static partial Regex CredentialRegex();
}
