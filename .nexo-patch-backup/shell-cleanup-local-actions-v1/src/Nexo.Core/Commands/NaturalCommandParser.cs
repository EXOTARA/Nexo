using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Nexo.Core.Commands;

public sealed partial class NaturalCommandParser
{
    public CommandInterpretation Parse(string? rawText)
    {
        var original = rawText?.Trim() ?? string.Empty;
        var normalized = Normalize(original);

        if (string.IsNullOrWhiteSpace(normalized))
        {
            return CommandInterpretation.ForAi(original, normalized);
        }

        // Se normaliza antes y después de quitar la palabra de activación para
        // aceptar frases como "oye Nexo, bájale a Spotify".
        normalized = SpanishCommandLexicon.NormalizeForParsing(normalized);
        normalized = RemoveWakeWord(normalized);
        normalized = SpanishCommandLexicon.NormalizeForParsing(normalized);

        if (string.IsNullOrWhiteSpace(normalized))
        {
            return CommandInterpretation.ForAi(original, normalized);
        }

        if (Matches(normalized,
                "muestra peek",
                "muestra el peek",
                "abre peek",
                "abre el peek",
                "ver peek",
                "modo peek",
                "vista peek",
                "vista rapida",
                "muestra el estado rapido"))
        {
            return Local(original, normalized, LocalCommandType.ShowPeek);
        }

        if (Matches(normalized,
                "mira esto",
                "mira la pantalla",
                "analiza esto",
                "analiza la pantalla",
                "revisa esto",
                "que ves",
                "que ves aqui",
                "explica este error",
                "ayudame con este error"))
        {
            return Local(original, normalized, LocalCommandType.CaptureForVision);
        }

        if (Matches(normalized,
                "que dia es hoy",
                "que fecha es hoy",
                "dime la fecha",
                "dime que dia es",
                "fecha de hoy",
                "dia de hoy"))
        {
            return Local(original, normalized, LocalCommandType.ShowCurrentDate);
        }

        if (Matches(normalized,
                "que hora es",
                "dime la hora",
                "hora actual",
                "que hora tenemos"))
        {
            return Local(original, normalized, LocalCommandType.ShowCurrentTime);
        }

        if (Matches(normalized,
                "como esta mi pc",
                "como esta mi computadora",
                "como anda mi pc",
                "como anda mi computadora",
                "estado de mi pc",
                "estado de mi computadora",
                "estado del equipo",
                "revisa mi pc",
                "revisa mi computadora",
                "revisa el equipo"))
        {
            return Local(original, normalized, LocalCommandType.ShowSystemStatus);
        }

        if (TryParseNavigation(normalized, out var navigationType))
        {
            return Local(original, normalized, navigationType);
        }

        if (Matches(normalized,
                "abre powershell",
                "abre el powershell",
                "inicia powershell",
                "power shell"))
        {
            return Local(original, normalized, LocalCommandType.OpenPowerShell);
        }

        if (Matches(normalized, "abre cmd", "abre simbolo del sistema", "inicia cmd"))
        {
            return Local(original, normalized, LocalCommandType.OpenCommandPrompt);
        }

        if (Matches(normalized, "abre terminal", "abre windows terminal", "inicia terminal"))
        {
            return Local(original, normalized, LocalCommandType.OpenWindowsTerminal);
        }

        var lowerAllMatch = LowerAllExceptRegex().Match(normalized);
        if (lowerAllMatch.Success)
        {
            return CommandInterpretation.ForLocal(
                original,
                normalized,
                new LocalCommandIntent(
                    LocalCommandType.LowerAllExcept,
                    CleanTarget(lowerAllMatch.Groups["target"].Value),
                    Factor: 0.5));
        }

        // Una cifra explícita siempre significa "dejar en ese nivel", incluso
        // cuando la frase empieza con baja/bajas/bájale.
        var setVolumeMatch = SetVolumeRegex().Match(normalized);
        if (setVolumeMatch.Success &&
            TryParsePercentage(setVolumeMatch.Groups["value"].Value, out var requestedPercent))
        {
            var clampedPercent = Math.Clamp(requestedPercent, 0, 100);
            return CommandInterpretation.ForLocal(
                original,
                normalized,
                new LocalCommandIntent(
                    LocalCommandType.SetApplicationVolume,
                    CleanTarget(setVolumeMatch.Groups["target"].Value),
                    Percent: clampedPercent));
        }

        var unmuteMatch = UnmuteRegex().Match(normalized);
        if (unmuteMatch.Success)
        {
            return CommandInterpretation.ForLocal(
                original,
                normalized,
                new LocalCommandIntent(
                    LocalCommandType.UnmuteApplication,
                    CleanTarget(unmuteMatch.Groups["target"].Value)));
        }

        var muteMatch = MuteRegex().Match(normalized);
        if (muteMatch.Success)
        {
            return CommandInterpretation.ForLocal(
                original,
                normalized,
                new LocalCommandIntent(
                    LocalCommandType.MuteApplication,
                    CleanTarget(muteMatch.Groups["target"].Value)));
        }

        var lowerSlightlyMatch = LowerSlightlyRegex().Match(normalized);
        if (lowerSlightlyMatch.Success)
        {
            return CommandInterpretation.ForLocal(
                original,
                normalized,
                new LocalCommandIntent(
                    LocalCommandType.ChangeApplicationVolume,
                    CleanTarget(lowerSlightlyMatch.Groups["target"].Value),
                    DeltaPoints: -10));
        }

        var lowerMatch = LowerVolumeRegex().Match(normalized);
        if (lowerMatch.Success)
        {
            return CommandInterpretation.ForLocal(
                original,
                normalized,
                new LocalCommandIntent(
                    LocalCommandType.ScaleApplicationVolume,
                    CleanTarget(lowerMatch.Groups["target"].Value),
                    Factor: 0.5));
        }

        var raiseMatch = RaiseVolumeRegex().Match(normalized);
        if (raiseMatch.Success)
        {
            return CommandInterpretation.ForLocal(
                original,
                normalized,
                new LocalCommandIntent(
                    LocalCommandType.ChangeApplicationVolume,
                    CleanTarget(raiseMatch.Groups["target"].Value),
                    DeltaPoints: 10));
        }

        return CommandInterpretation.ForAi(original, normalized);
    }

    public static string Normalize(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var decomposed = text.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);

        foreach (var character in decomposed)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(character);
            if (category == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            builder.Append(char.IsLetterOrDigit(character) ? character : ' ');
        }

        return WhitespaceRegex().Replace(builder.ToString(), " ").Trim();
    }

    private static bool TryParseNavigation(string text, out LocalCommandType type)
    {
        type = LocalCommandType.None;

        if (Matches(text, "abre ia", "muestra ia", "ve a ia", "abre asistente", "muestra asistente"))
        {
            type = LocalCommandType.NavigateAssistant;
            return true;
        }

        if (Matches(text, "abre audio", "muestra audio", "ve a audio", "abre mezclador"))
        {
            type = LocalCommandType.NavigateAudio;
            return true;
        }

        if (Matches(text, "abre captura", "muestra captura", "ve a captura", "abre capturas"))
        {
            type = LocalCommandType.NavigateCapture;
            return true;
        }

        if (Matches(text, "abre sistema", "muestra sistema", "ve a sistema", "abre rendimiento"))
        {
            type = LocalCommandType.NavigateSystem;
            return true;
        }

        if (Matches(text, "abre ajustes", "muestra ajustes", "ve a ajustes", "abre configuracion"))
        {
            type = LocalCommandType.NavigateSettings;
            return true;
        }

        return false;
    }

    private static CommandInterpretation Local(
        string original,
        string normalized,
        LocalCommandType type) =>
        CommandInterpretation.ForLocal(original, normalized, new LocalCommandIntent(type));

    private static bool Matches(string value, params string[] options) =>
        options.Any(option => value.Equals(option, StringComparison.OrdinalIgnoreCase));

    private static string RemoveWakeWord(string text)
    {
        if (text.Equals("nexo", StringComparison.OrdinalIgnoreCase) ||
            text.Equals("exo", StringComparison.OrdinalIgnoreCase))
        {
            return string.Empty;
        }

        if (text.StartsWith("nexo ", StringComparison.OrdinalIgnoreCase))
        {
            return text[5..].Trim();
        }

        if (text.StartsWith("exo ", StringComparison.OrdinalIgnoreCase))
        {
            return text[4..].Trim();
        }

        return text;
    }

    private static string CleanTarget(string target)
    {
        var cleaned = target.Trim();
        cleaned = Regex.Replace(cleaned, @"^(?:a|de|el|la|los|las)\s+", string.Empty);
        cleaned = Regex.Replace(cleaned, @"\s+(?:por favor)$", string.Empty);
        cleaned = cleaned.Trim();

        return SpanishCommandLexicon.NormalizeTarget(cleaned);
    }

    private static bool TryParsePercentage(string value, out double percentage)
    {
        var normalized = Normalize(value);
        if (double.TryParse(
                normalized,
                NumberStyles.Number,
                CultureInfo.InvariantCulture,
                out percentage))
        {
            return true;
        }

        percentage = normalized switch
        {
            "cero" => 0,
            "cinco" => 5,
            "diez" => 10,
            "quince" => 15,
            "veinte" => 20,
            "veinticinco" => 25,
            "treinta" => 30,
            "treinta y cinco" => 35,
            "cuarenta" => 40,
            "cuarenta y cinco" => 45,
            "cincuenta" => 50,
            "cincuenta y cinco" => 55,
            "sesenta" => 60,
            "sesenta y cinco" => 65,
            "setenta" => 70,
            "setenta y cinco" => 75,
            "ochenta" => 80,
            "ochenta y cinco" => 85,
            "noventa" => 90,
            "noventa y cinco" => 95,
            "cien" or "maximo" or "maxima" => 100,
            _ => double.NaN
        };

        return !double.IsNaN(percentage);
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();

    [GeneratedRegex(@"^(?:baja|reduce)\s+todo\s+menos\s+(?<target>.+)$", RegexOptions.IgnoreCase)]
    private static partial Regex LowerAllExceptRegex();

    [GeneratedRegex(@"^(?:pon|ajusta|establece|deja|sube|aumenta|baja|reduce)\s+(?:a\s+)?(?:el\s+)?(?:volumen\s+(?:de|a)\s+)?(?<target>.+?)\s+(?:a|al|en|hasta)\s+(?<value>\d{1,3}|[a-z]+(?:\s+y\s+[a-z]+)?)(?:\s+por\s+ciento)?$", RegexOptions.IgnoreCase)]
    private static partial Regex SetVolumeRegex();

    [GeneratedRegex(@"^(?:quita\s+el\s+silencio\s+de|desmutea|activa\s+el\s+sonido\s+de|devuelve\s+el\s+sonido\s+a)\s+(?<target>.+)$", RegexOptions.IgnoreCase)]
    private static partial Regex UnmuteRegex();

    [GeneratedRegex(@"^(?:silencia|mutea|apaga\s+el\s+sonido\s+de)\s+(?<target>.+)$", RegexOptions.IgnoreCase)]
    private static partial Regex MuteRegex();

    [GeneratedRegex(@"^(?:baja|reduce)\s+(?:un\s+poco|poquito|un\s+poquito)\s+(?:a\s+)?(?:el\s+)?(?:volumen\s+(?:de|a)\s+)?(?<target>.+)$", RegexOptions.IgnoreCase)]
    private static partial Regex LowerSlightlyRegex();

    [GeneratedRegex(@"^(?:baja|reduce)\s+(?:a\s+)?(?:el\s+)?(?:volumen\s+(?:de|a)\s+)?(?<target>.+?)(?:\s+a\s+la\s+mitad)?$", RegexOptions.IgnoreCase)]
    private static partial Regex LowerVolumeRegex();

    [GeneratedRegex(@"^(?:sube|aumenta)\s+(?:un\s+poco\s+)?(?:a\s+)?(?:el\s+)?(?:volumen\s+(?:de|a)\s+)?(?<target>.+)$", RegexOptions.IgnoreCase)]
    private static partial Regex RaiseVolumeRegex();
}
