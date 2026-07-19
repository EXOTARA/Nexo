using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Nexo.Core.Focus;

public sealed partial class SpanishFocusCommandParser
{
    private static readonly IReadOnlyDictionary<string, int> NumberWords =
        new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["un"] = 1,
            ["una"] = 1,
            ["uno"] = 1,
            ["dos"] = 2,
            ["tres"] = 3,
            ["cuatro"] = 4,
            ["cinco"] = 5,
            ["diez"] = 10,
            ["quince"] = 15,
            ["veinte"] = 20,
            ["veinticinco"] = 25,
            ["treinta"] = 30,
            ["cuarenta"] = 40,
            ["cincuenta"] = 50,
            ["sesenta"] = 60,
            ["noventa"] = 90
        };

    public FocusCommand Parse(string? rawText)
    {
        var original = rawText?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(original))
        {
            return FocusCommand.None(original);
        }

        var normalized = Normalize(original);
        normalized = WakeWordRegex().Replace(normalized, string.Empty, 1).Trim();

        if (OpenRegex().IsMatch(normalized))
        {
            return new FocusCommand(FocusCommandType.OpenFocus, original);
        }

        if (PauseRegex().IsMatch(normalized))
        {
            return new FocusCommand(FocusCommandType.Pause, original);
        }

        if (ResumeRegex().IsMatch(normalized))
        {
            return new FocusCommand(FocusCommandType.Resume, original);
        }

        if (CancelRegex().IsMatch(normalized))
        {
            return new FocusCommand(FocusCommandType.Cancel, original);
        }

        if (StatusRegex().IsMatch(normalized))
        {
            return new FocusCommand(FocusCommandType.Status, original);
        }

        if (!StartRegex().IsMatch(normalized))
        {
            return FocusCommand.None(original);
        }

        var kind = ResolveKind(normalized);
        var duration = ParseDuration(normalized);
        if (!duration.HasValue)
        {
            duration = normalized.Contains("pomodoro", StringComparison.OrdinalIgnoreCase)
                ? TimeSpan.FromMinutes(25)
                : kind == FocusSessionKind.Break
                    ? TimeSpan.FromMinutes(5)
                    : null;
        }

        if (!duration.HasValue)
        {
            return FocusCommand.None(original);
        }

        var label = kind switch
        {
            FocusSessionKind.Study => "Sesión de estudio",
            FocusSessionKind.Focus => "Sesión de enfoque",
            FocusSessionKind.Break => "Descanso",
            _ => "Temporizador"
        };

        return new FocusCommand(
            FocusCommandType.Start,
            original,
            duration,
            label,
            kind);
    }

    private static TimeSpan? ParseDuration(string normalized)
    {
        var match = DurationRegex().Match(normalized);
        if (!match.Success)
        {
            return null;
        }

        var amountToken = match.Groups["amount"].Value;
        if (!int.TryParse(amountToken, NumberStyles.None, CultureInfo.InvariantCulture, out var amount) &&
            !NumberWords.TryGetValue(amountToken, out amount))
        {
            return null;
        }

        if (amount <= 0)
        {
            return null;
        }

        var unit = match.Groups["unit"].Value;
        if (unit.StartsWith("hora", StringComparison.OrdinalIgnoreCase))
        {
            return TimeSpan.FromHours(amount);
        }

        if (unit.StartsWith("segundo", StringComparison.OrdinalIgnoreCase))
        {
            return TimeSpan.FromSeconds(amount);
        }

        return TimeSpan.FromMinutes(amount);
    }

    private static FocusSessionKind ResolveKind(string normalized)
    {
        if (normalized.Contains("descanso", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("pausa de", StringComparison.OrdinalIgnoreCase))
        {
            return FocusSessionKind.Break;
        }

        if (normalized.Contains("estudio", StringComparison.OrdinalIgnoreCase))
        {
            return FocusSessionKind.Study;
        }

        if (normalized.Contains("enfoque", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("pomodoro", StringComparison.OrdinalIgnoreCase))
        {
            return FocusSessionKind.Focus;
        }

        return FocusSessionKind.Custom;
    }

    private static string Normalize(string value)
    {
        var decomposed = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(char.ToLowerInvariant(character));
            }
        }

        var withoutPunctuation = Regex.Replace(builder.ToString(), @"[^a-z0-9\s]", " ");
        return Regex.Replace(withoutPunctuation, @"\s+", " ").Trim();
    }

    [GeneratedRegex(@"^(?:oye\s+)?(?:nexo|exo)\s+")]
    private static partial Regex WakeWordRegex();

    [GeneratedRegex(@"^(?:abre|muestra|ve\s+a)\s+(?:el\s+)?(?:enfoque|temporizador|sesiones?\s+de\s+enfoque)$")]
    private static partial Regex OpenRegex();

    [GeneratedRegex(@"^(?:pausa|pausar|deten\s+por\s+ahora)\s+(?:el\s+|la\s+)?(?:temporizador|sesion|sesion\s+de\s+enfoque|sesion\s+de\s+estudio)?$")]
    private static partial Regex PauseRegex();

    [GeneratedRegex(@"^(?:continua|continuar|reanuda|reanudar|retoma|retomar)\s+(?:el\s+|la\s+)?(?:temporizador|sesion)?$")]
    private static partial Regex ResumeRegex();

    [GeneratedRegex(@"^(?:cancela|cancelar|deten|detener|termina|terminar)\s+(?:el\s+|la\s+)?(?:temporizador|sesion|sesion\s+de\s+enfoque|sesion\s+de\s+estudio)$")]
    private static partial Regex CancelRegex();

    [GeneratedRegex(@"^(?:cuanto\s+tiempo\s+me\s+queda|que\s+tiempo\s+queda|estado\s+del\s+temporizador|como\s+va\s+(?:el\s+)?temporizador)$")]
    private static partial Regex StatusRegex();

    [GeneratedRegex(@"^(?:inicia|iniciar|comienza|comenzar|pon|crea)\b.*(?:temporizador|sesion|enfoque|estudio|descanso|pomodoro)\b|^(?:temporizador|pomodoro)\b")]
    private static partial Regex StartRegex();

    [GeneratedRegex(@"(?<amount>\d{1,3}|un|una|uno|dos|tres|cuatro|cinco|diez|quince|veinte|veinticinco|treinta|cuarenta|cincuenta|sesenta|noventa)\s*(?<unit>segundos?|minutos?|horas?)\b")]
    private static partial Regex DurationRegex();
}
