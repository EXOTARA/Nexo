using System.Text;

namespace Nexo.Core.Workspace;

/// <summary>
/// Diseño D12 (Fase 5) — arma el contexto del proyecto que acompaña a la consulta. Igual que el de
/// memoria (D10), se trata como un ENVÍO: cuando el proveedor de IA es remoto, esto sale del equipo.
///
/// Por eso el resumen lleva estructura y no contenido. Enviar el árbol de archivos y unas pocas
/// líneas de código deja explicar de qué va un proyecto sin mandar el proyecto entero a un servidor
/// ajeno. Lo que sí se envía pasa antes por <see cref="WorkspaceSecretScanner"/>, y cuando algo se
/// redacta **se dice**: un secreto silenciosamente tapado haría creer que no había ninguno.
/// </summary>
public static class WorkspaceContextBuilder
{
    public const int MaximumFilesListed = 40;

    public const int MaximumCharacters = 4000;

    public static string? BuildStructure(
        string authorizedRoot,
        IReadOnlyList<WorkspaceFile> files,
        WorkspaceAutonomyLevel level)
    {
        if (string.IsNullOrWhiteSpace(authorizedRoot) || files is null || files.Count == 0)
        {
            return null;
        }

        var builder = new StringBuilder();
        builder.Append("Proyecto autorizado: ").AppendLine(Path.GetFileName(
            Path.TrimEndingDirectorySeparator(authorizedRoot)));
        builder.Append("Nivel de autonomía: ").AppendLine(WorkspaceAutonomyPolicy.Describe(level));
        builder.AppendLine(
            "No puedes modificar archivos: describe, explica y guía, pero los cambios los aplica la persona.");
        builder.AppendLine("Archivos del proyecto:");

        var listed = 0;
        foreach (var file in files.Take(MaximumFilesListed))
        {
            var line = $"- {file.RelativePath}";
            if (builder.Length + line.Length > MaximumCharacters)
            {
                break;
            }

            builder.AppendLine(line);
            listed++;
        }

        if (files.Count > listed)
        {
            builder.Append("(y ").Append(files.Count - listed).AppendLine(" archivos más)");
        }

        return builder.ToString().TrimEnd();
    }

    /// <summary>
    /// Añade el contenido de archivos concretos, ya redactados. Se corta por archivo entero: medio
    /// archivo de código es peor que ninguno, porque invita a razonar sobre lo que no se ve.
    /// </summary>
    public static string BuildFileExcerpts(IReadOnlyList<WorkspaceReadResult> files)
    {
        if (files is null || files.Count == 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        var redacted = 0;

        foreach (var file in files.Where(file => file.Success))
        {
            var block = $"--- {file.RelativePath} ---{Environment.NewLine}{file.Content}";
            if (builder.Length + block.Length > MaximumCharacters)
            {
                break;
            }

            builder.AppendLine(block);
            if (file.SecretsRedacted)
            {
                redacted++;
            }
        }

        if (redacted > 0)
        {
            builder.AppendLine(redacted == 1
                ? "Nota: en 1 archivo se ocultó un valor que parecía un secreto."
                : $"Nota: en {redacted} archivos se ocultaron valores que parecían secretos.");
        }

        return builder.ToString().TrimEnd();
    }
}
