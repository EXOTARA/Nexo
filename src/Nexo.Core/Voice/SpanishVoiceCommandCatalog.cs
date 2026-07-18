namespace Nexo.Core.Voice;

public static class SpanishVoiceCommandCatalog
{
    private static readonly string[] Applications =
    [
        "discord",
        "spotify",
        "zen",
        "firefox",
        "chrome",
        "edge",
        "sonidos del sistema"
    ];

    private static readonly IReadOnlyDictionary<int, string> PercentWords =
        new Dictionary<int, string>
        {
            [0] = "cero",
            [5] = "cinco",
            [10] = "diez",
            [15] = "quince",
            [20] = "veinte",
            [25] = "veinticinco",
            [30] = "treinta",
            [35] = "treinta y cinco",
            [40] = "cuarenta",
            [45] = "cuarenta y cinco",
            [50] = "cincuenta",
            [55] = "cincuenta y cinco",
            [60] = "sesenta",
            [65] = "sesenta y cinco",
            [70] = "setenta",
            [75] = "setenta y cinco",
            [80] = "ochenta",
            [85] = "ochenta y cinco",
            [90] = "noventa",
            [95] = "noventa y cinco",
            [100] = "cien"
        };

    public static IReadOnlyList<string> CreatePhrases()
    {
        var basePhrases = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "cómo está mi pc",
            "como esta mi pc",
            "cómo está mi computadora",
            "como esta mi computadora",
            "muestra peek",
            "muestra la vista rápida",
            "abre inteligencia artificial",
            "abre ia",
            "abre audio",
            "abre captura",
            "abre sistema",
            "abre ajustes",
            "abre powershell",
            "abre power shell",
            "abre cmd",
            "abre terminal"
        };

        foreach (var application in Applications)
        {
            basePhrases.Add($"baja {application}");
            basePhrases.Add($"baja un poco {application}");
            basePhrases.Add($"sube {application}");
            basePhrases.Add($"sube un poco {application}");
            basePhrases.Add($"silencia {application}");
            basePhrases.Add($"quita el silencio de {application}");
            basePhrases.Add($"reactiva {application}");
            basePhrases.Add($"baja todo menos {application}");

            foreach (var percentage in PercentWords.Values)
            {
                basePhrases.Add($"pon {application} al {percentage} por ciento");
                basePhrases.Add($"pon el volumen de {application} al {percentage} por ciento");
            }
        }

        var allPhrases = new HashSet<string>(basePhrases, StringComparer.OrdinalIgnoreCase);
        foreach (var phrase in basePhrases)
        {
            allPhrases.Add($"nexo {phrase}");
            allPhrases.Add($"exo {phrase}");
        }

        return allPhrases
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}
