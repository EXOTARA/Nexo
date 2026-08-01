using System.Text;
using Nexo.Core.Vision;

namespace Nexo.Core.Memory;

/// <summary>
/// Diseño D10 (Fase 6) — convierte lo recordado en el bloque de contexto que acompaña a la consulta.
/// Es lo que hace útil a la memoria: guardarla sin usarla no da continuidad ninguna.
///
/// Tres reglas, todas con el mismo motivo de fondo — este texto SALE del equipo cuando el proveedor
/// de IA es remoto, así que se trata como un envío, no como una lectura interna:
///
/// 1. **Solo categorías activadas ahora mismo.** Una entrada de una categoría que la persona acaba
///    de desactivar sigue guardada (borrarla sería destruir datos por mover un interruptor, y para
///    eso está "olvidar"), pero deja de usarse en el acto. Desactivar tiene que surtir efecto ya.
/// 2. **Se vuelve a redactar al salir.** Lo guardado ya pasó por <see cref="MemoryPolicy"/>, pero la
///    comprobación se repite aquí: es barata y protege de cualquier entrada que hubiera llegado por
///    una versión anterior con criterios más laxos.
/// 3. **Cantidad y tamaño acotados.** Un contexto que crece sin límite acaba desplazando a la
///    propia pregunta y encarece cada consulta.
/// </summary>
public static class MemoryContextBuilder
{
    public const int MaximumEntries = 12;

    public const int MaximumCharacters = 1200;

    public static string? Build(
        IEnumerable<MemoryEntry> entries,
        MemorySettings settings,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(settings);

        if (!settings.Enabled || entries is null)
        {
            return null;
        }

        var usable = MemoryPolicy.ApplyRetention(entries, settings, now)
            .Where(entry => settings.IsCategoryEnabled(entry.Category))
            .Where(entry => !SensitiveContentRedactor.ContainsSensitiveContent(entry.Text))
            .Take(MaximumEntries)
            .ToArray();

        if (usable.Length == 0)
        {
            return null;
        }

        var builder = new StringBuilder();
        builder.AppendLine(
            "Memoria personal que el usuario autorizó guardar. Úsala si viene al caso y no la " +
            "menciones si no aporta nada:");

        var written = 0;
        foreach (var entry in usable)
        {
            var line = $"- [{MemoryPolicy.CategoryLabel(entry.Category)}] {entry.Text}";

            // El corte es por bloque entero, no a media línea: una entrada truncada podría cambiar
            // de sentido ("prefiero que no me avises" → "prefiero que no me").
            if (builder.Length + line.Length > MaximumCharacters)
            {
                break;
            }

            builder.AppendLine(line);
            written++;
        }

        // Si no cupo ni una entrada, el encabezado solo anunciaría una memoria vacía. Mejor no
        // mandar nada que mandar una promesa sin contenido.
        return written == 0 ? null : builder.ToString().TrimEnd();
    }
}
