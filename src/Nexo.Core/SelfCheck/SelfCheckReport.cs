using System.Text;

namespace Nexo.Core.SelfCheck;

/// <summary>
/// Diseño D22 — arma el informe legible. La parte que más importa es la última sección: **qué NO se
/// comprobó**.
///
/// Un informe todo en verde invita a concluir "está todo bien", y aquí eso sería falso: nada de esto
/// mira la interfaz, ni comprueba que el dictado escriba donde toca, ni que la píldora no robe el
/// foco. Si el informe no lo dice, la primera vez que alguien lo lea sacará la conclusión
/// equivocada, y será culpa del informe.
/// </summary>
public static class SelfCheckReport
{
    public static string Build(IReadOnlyList<SelfCheckResult> results)
    {
        ArgumentNullException.ThrowIfNull(results);

        var failed = results.Count(result => result.Status == SelfCheckStatus.Fallo);
        var warned = results.Count(result => result.Status == SelfCheckStatus.Aviso);
        var skipped = results.Count(result => result.Status == SelfCheckStatus.NoComprobado);

        var builder = new StringBuilder();
        builder.AppendLine(Summarize(results.Count, failed, warned, skipped));
        builder.AppendLine();

        // Los fallos primero: si hay algo roto, es lo que la persona necesita ver sin desplazarse.
        foreach (var result in results.OrderBy(result => SortKey(result.Status)))
        {
            builder.Append(Symbol(result.Status)).Append(' ').AppendLine(result.Title);
            builder.Append("   Comprueba: ").AppendLine(result.WhatItProves);

            if (!string.IsNullOrWhiteSpace(result.Detail))
            {
                builder.Append("   ").AppendLine(result.Detail);
            }
        }

        builder.AppendLine();
        builder.AppendLine("── Qué NO comprueba esto ──");
        builder.AppendLine("· Que la interfaz se vea bien y los botones hagan lo que dicen.");
        builder.AppendLine("· Que el dictado escriba en la ventana correcta.");
        builder.AppendLine("· Que la píldora no robe el foco a lo que estés haciendo.");
        builder.AppendLine("· Que Sakura entienda tu voz en tu micrófono.");
        builder.AppendLine();
        builder.AppendLine(
            "Todo eso solo lo comprueba una persona usando Sakura un rato. Esto reduce ese trabajo; " +
            "no lo sustituye.");

        return builder.ToString().TrimEnd();
    }

    private static string Summarize(int total, int failed, int warned, int skipped)
    {
        if (failed > 0)
        {
            return $"Comprobé {total} cosas y {failed} NO funcionan. Empieza por ésas.";
        }

        if (warned > 0 || skipped > 0)
        {
            return $"Comprobé {total} cosas: ninguna falla, pero {warned + skipped} merecen un vistazo.";
        }

        return $"Comprobé {total} cosas y todas funcionan en este equipo.";
    }

    private static int SortKey(SelfCheckStatus status) => status switch
    {
        SelfCheckStatus.Fallo => 0,
        SelfCheckStatus.Aviso => 1,
        SelfCheckStatus.NoComprobado => 2,
        _ => 3
    };

    private static string Symbol(SelfCheckStatus status) => status switch
    {
        SelfCheckStatus.Fallo => "[FALLA]",
        SelfCheckStatus.Aviso => "[AVISO]",
        SelfCheckStatus.NoComprobado => "[  ?  ]",
        _ => "[  OK ]"
    };
}
