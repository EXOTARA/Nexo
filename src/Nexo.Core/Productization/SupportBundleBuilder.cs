using System.Text;
using Nexo.Core.Audit;
using Nexo.Core.Diagnostics;
using Nexo.Core.Vision;
using Nexo.Core.Workspace;

namespace Nexo.Core.Productization;

/// <summary>
/// Diseño D21 (Fase 9) — el paquete de soporte exportable que pide el criterio de terminado de la
/// fase: *"diagnóstico exportable para soporte"*.
///
/// Se trata como lo que es: **un envío**. Va a salir del equipo y a llegar a otra persona, así que
/// vale la misma disciplina que el contexto de memoria (D10) o el del proyecto (D12):
///
/// - **Todo lo que entra se redacta**, con las dos herramientas y no con una: el redactor de datos
///   personales (contraseñas, tarjetas, tokens) y el escáner de secretos de código (claves de API,
///   cadenas de conexión). Buscan cosas distintas y aquí puede haber de las dos.
/// - **Las rutas pierden el nombre de usuario.** Es la fuga fácil de pasar por alto: sin esto, cada
///   línea con una ruta revela cómo se llama la persona.
/// - **El contenido personal no entra.** Ni memoria, ni conversaciones, ni código del proyecto. Del
///   registro de actividad entra QUÉ se hizo, que es lo útil para soporte, no sobre qué.
/// - **Se dice lo que se dejó fuera.** Un paquete que omite en silencio obliga a confiar; uno que
///   enumera lo omitido se puede revisar. Y la persona debería poder revisar lo que manda.
/// </summary>
public static class SupportBundleBuilder
{
    /// <summary>Entradas de auditoría incluidas. Suficientes para explicar un problema reciente.</summary>
    public const int AuditEntriesIncluded = 40;

    public static string Build(
        NexoDiagnosticSnapshot diagnostics,
        IReadOnlyList<AuditEntry> auditEntries,
        Func<string, bool> fileExists)
    {
        ArgumentNullException.ThrowIfNull(diagnostics);
        ArgumentNullException.ThrowIfNull(fileExists);

        var builder = new StringBuilder();

        builder.AppendLine("Kohana · paquete de soporte");
        builder.Append("Generado: ").AppendLine(DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm"));
        builder.AppendLine();

        builder.AppendLine("── Equipo y versiones ──");
        builder.Append("Versión de Kohana: ").AppendLine(diagnostics.AppVersion);
        builder.Append("Sistema: ").AppendLine(diagnostics.OperatingSystem);
        builder.Append("Runtime: ").AppendLine(diagnostics.RuntimeVersion);
        builder.Append("Carpeta de datos: ").AppendLine(PathRedactor.Shorten(diagnostics.DataDirectory));
        builder.AppendLine();

        builder.AppendLine("── Estado de los subsistemas ──");
        foreach (var item in diagnostics.Items)
        {
            builder.Append('[').Append(item.Status).Append("] ")
                .Append(item.Name).Append(": ")
                .AppendLine(Sanitize(item.Detail));
        }

        builder.AppendLine();
        builder.AppendLine("── Qué hay guardado (solo si existe y cuál es, nunca su contenido) ──");
        foreach (var item in KohanaDataInventory.All)
        {
            builder.Append("· ").Append(item.FileName).Append(": ")
                .AppendLine(fileExists(item.FullPath) ? "existe" : "no existe");
        }

        builder.AppendLine();
        builder.Append("── Últimas ").Append(AuditEntriesIncluded).AppendLine(" acciones ──");

        var entries = auditEntries?.Take(AuditEntriesIncluded).ToArray() ?? [];
        if (entries.Length == 0)
        {
            builder.AppendLine("(ninguna)");
        }
        else
        {
            foreach (var entry in entries)
            {
                builder.Append(entry.At.ToString("yyyy-MM-dd HH:mm")).Append(" · ")
                    .Append(entry.Capability).Append(" · ")
                    .Append(entry.Action).Append(": ")
                    .AppendLine(Sanitize(entry.Detail));
            }
        }

        builder.AppendLine();
        builder.AppendLine("── Qué NO se incluye en este archivo ──");
        builder.AppendLine("· Lo que Kohana recuerda de ti (contenido de la memoria personal).");
        builder.AppendLine("· Tus conversaciones.");
        builder.AppendLine("· El código o los archivos de tu proyecto.");
        builder.AppendLine("· El nombre de tu carpeta de usuario en las rutas.");
        builder.AppendLine("· Contraseñas, tokens y claves, si alguno hubiera aparecido en un mensaje.");
        builder.AppendLine();
        builder.AppendLine("Puedes abrir este archivo y leerlo entero antes de mandárselo a nadie.");

        return builder.ToString().TrimEnd();
    }

    /// <summary>
    /// Las dos redacciones, no una. <see cref="SensitiveContentRedactor"/> busca datos personales;
    /// <see cref="WorkspaceSecretScanner"/> busca secretos con forma de configuración. Un detalle de
    /// diagnóstico puede traer de los dos tipos —una URL con credenciales incrustadas, por ejemplo—
    /// y cada herramienta ve lo que la otra no.
    /// </summary>
    private static string Sanitize(string? text) =>
        PathRedactor.Shorten(
            WorkspaceSecretScanner.Redact(
                SensitiveContentRedactor.Redact(text ?? string.Empty)));
}
