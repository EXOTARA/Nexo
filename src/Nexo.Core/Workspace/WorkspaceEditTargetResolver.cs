using System.Text.RegularExpressions;

namespace Nexo.Core.Workspace;

/// <summary>
/// Corrección de un defecto real, encontrado probando D14 a mano: pedir "agrega un comentario al
/// README" hacía que el modelo **sustituyera el archivo entero** por una sola línea, en vez de
/// añadirla. La causa era que el flujo de edición nunca le enseñaba al modelo el contenido ACTUAL
/// del archivo — solo la lista de nombres (<see cref="WorkspaceContextBuilder.BuildStructure"/>).
/// Como el formato <c>KOHANA-EDIT</c> exige el archivo COMPLETO (D14, a propósito, para poder
/// verificar releyendo), un modelo sin el original solo puede inventarlo.
///
/// Este resolver decide, **por coincidencia de nombre, no por IA**, a qué archivos existentes se
/// refiere una instrucción en español ("el README", "actualiza script.js"), para poder leer su
/// contenido de verdad antes de pedir el cambio. Es determinista y se puede probar sin un modelo de
/// por medio, que es justo lo que hace falta para confiar en él: si esto adivinara mal, el remedio
/// sería peor que la enfermedad original.
///
/// **Ante la duda, no se incluye nada.** Un nombre que no aparece con claridad en la instrucción no
/// se añade por parecido ni por estar "probablemente relacionado": incluir el archivo equivocado le
/// daría al modelo contenido que nadie pidió compartir, y no resolvería el problema para el archivo
/// que sí importaba.
/// </summary>
public static class WorkspaceEditTargetResolver
{
    /// <summary>Tope de archivos cuyo contenido se ofrece de una vez. Nombrar cinco archivos en una frase ya es mucho pedir.</summary>
    public const int MaximumMatches = 5;

    public static IReadOnlyList<WorkspaceFile> Resolve(string? prompt, IReadOnlyList<WorkspaceFile>? files)
    {
        if (string.IsNullOrWhiteSpace(prompt) || files is null || files.Count == 0)
        {
            return [];
        }

        var normalizedPrompt = prompt.ToLowerInvariant();

        return files
            .Where(file => MentionsFile(normalizedPrompt, file.RelativePath))
            .Take(MaximumMatches)
            .ToArray();
    }

    private static bool MentionsFile(string normalizedPrompt, string relativePath)
    {
        var fileName = Path.GetFileName(relativePath).ToLowerInvariant();
        if (fileName.Length > 0 && normalizedPrompt.Contains(fileName, StringComparison.Ordinal))
        {
            return true;
        }

        // También sin extensión: "actualiza el readme" nombra el archivo igual de claro que
        // "actualiza README.md". Con límite de palabra completa para no confundir "app" con
        // "aplicación" o cualquier substring casual de tres letras.
        var withoutExtension = Path.GetFileNameWithoutExtension(relativePath).ToLowerInvariant();
        if (withoutExtension.Length < 3)
        {
            return false;
        }

        return Regex.IsMatch(normalizedPrompt, $@"\b{Regex.Escape(withoutExtension)}\b");
    }
}
