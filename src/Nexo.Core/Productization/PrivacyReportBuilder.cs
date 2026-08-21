using System.Text;

namespace Nexo.Core.Productization;

/// <summary>
/// Diseño D21 (Fase 9) — el informe de privacidad: qué guarda Sakura, dónde, si está cifrado y cómo
/// borrarlo.
///
/// Se construye sobre <see cref="SakuraDataInventory"/> y no sobre una lista propia, que es el punto:
/// si alguien añade un archivo nuevo y no lo describe en el inventario, no aparecerá aquí — y esa
/// omisión se ve al leer el informe, en vez de quedarse escondida en el código.
///
/// **No incluye el contenido de nada.** Dice que existe una memoria personal y dónde está; no dice
/// qué recuerda. Un informe de privacidad que enseñara los datos para demostrar que los guarda sería
/// una contradicción con patas.
/// </summary>
public static class PrivacyReportBuilder
{
    public static string Build(string dataDirectory, Func<string, bool> fileExists, Func<string, long> fileSize)
    {
        ArgumentNullException.ThrowIfNull(fileExists);
        ArgumentNullException.ThrowIfNull(fileSize);

        var builder = new StringBuilder();
        builder.AppendLine("Sakura · qué guarda en tu equipo");
        builder.AppendLine();
        builder.Append("Todo está en: ").AppendLine(PathRedactor.Shorten(dataDirectory));
        builder.AppendLine("Nada de esto sale de tu equipo salvo que tú lo pidas.");
        builder.AppendLine();

        var personal = SakuraDataInventory.Personal;
        var operational = SakuraDataInventory.All.Where(item => !item.IsPersonal).ToArray();

        builder.AppendLine("── Datos tuyos ──");
        AppendItems(builder, personal, fileExists, fileSize);

        builder.AppendLine();
        builder.AppendLine("── Datos de funcionamiento (Sakura los rehace sola) ──");
        AppendItems(builder, operational, fileExists, fileSize);

        builder.AppendLine();
        builder.AppendLine("── Cómo borrarlo ──");
        builder.AppendLine("· La memoria personal: «Olvidar todo lo que Sakura recuerda».");
        builder.AppendLine("· El acceso a tu proyecto: «Revocar el acceso al proyecto».");
        builder.AppendLine("· Todo de golpe: desinstala Sakura y borra la carpeta de arriba.");
        builder.AppendLine();
        builder.AppendLine(
            "Este informe dice QUÉ se guarda y dónde. No incluye el contenido de nada: enseñar tus " +
            "datos para demostrar que se guardan sería contradictorio.");

        return builder.ToString().TrimEnd();
    }

    private static void AppendItems(
        StringBuilder builder,
        IReadOnlyList<SakuraDataItem> items,
        Func<string, bool> fileExists,
        Func<string, long> fileSize)
    {
        foreach (var item in items)
        {
            var exists = fileExists(item.FullPath);

            builder.Append("· ").Append(item.Title).Append(" (").Append(item.FileName).Append(')');
            builder.AppendLine(exists ? string.Empty : " — no existe todavía");
            builder.Append("  ").AppendLine(item.WhatItHolds);

            if (exists)
            {
                builder.Append("  ")
                    .Append(FormatSize(fileSize(item.FullPath)))
                    .AppendLine(item.IsEncrypted ? " · cifrado" : " · sin cifrar");
            }
        }
    }

    private static string FormatSize(long bytes) => bytes switch
    {
        < 1024 => $"{bytes} bytes",
        < 1024 * 1024 => $"{bytes / 1024.0:0.#} KB",
        _ => $"{bytes / (1024.0 * 1024.0):0.#} MB"
    };
}
