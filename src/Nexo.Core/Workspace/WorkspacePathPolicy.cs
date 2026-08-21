namespace Nexo.Core.Workspace;

/// <summary>Por qué una ruta no puede leerse. Cada motivo se le dice a la persona tal cual.</summary>
public enum WorkspaceAccessDenial
{
    Permitido,

    /// <summary>No hay ninguna carpeta autorizada. Es el estado inicial y el de después de revocar.</summary>
    SinAutorizacion,

    /// <summary>La ruta cae fuera de la carpeta autorizada.</summary>
    FueraDelWorkspace,

    /// <summary>Carpeta excluida (dependencias, artefactos de compilación, .git…).</summary>
    CarpetaExcluida,

    /// <summary>Extensión que no es texto de proyecto.</summary>
    TipoNoSoportado,

    /// <summary>Archivo demasiado grande para leerse entero.</summary>
    DemasiadoGrande,

    /// <summary>El nombre delata un archivo de secretos (.env, claves privadas, almacenes de certificados).</summary>
    ArchivoDeSecretos
}

public sealed record WorkspaceAccessVerdict(WorkspaceAccessDenial Denial, string Message)
{
    public bool IsAllowed => Denial == WorkspaceAccessDenial.Permitido;

    public static WorkspaceAccessVerdict Allowed { get; } =
        new(WorkspaceAccessDenial.Permitido, "Permitido.");

    public static WorkspaceAccessVerdict Denied(WorkspaceAccessDenial denial, string message) =>
        new(denial, message);
}

/// <summary>
/// Diseño D12 (Fase 5) — decide si una ruta concreta puede leerse. Es la pieza de seguridad del
/// sprint: el roadmap dice que el acceso es *"a un workspace explícitamente autorizado, nunca a todo
/// el disco"*, y esta clase es lo que hace que esa frase signifique algo.
///
/// Tres decisiones que importan:
///
/// 1. **La contención se comprueba sobre rutas ya resueltas**, no sobre las cadenas que llegan. Una
///    comprobación textual la burla cualquier <c>..\..\</c>, y también los enlaces simbólicos y las
///    uniones de directorio de Windows, que apuntan fuera sin que la ruta lo aparente.
/// 2. **La comparación exige separador**: <c>C:\Proyectos\Sakura</c> no contiene a
///    <c>C:\Proyectos\Sakura-privado</c>, aunque una comparación de prefijos diría que sí.
/// 3. **Todo se niega salvo lo que se permite explícitamente.** La lista de extensiones es una
///    lista de permitidos, no de prohibidos: lo que no se reconoce no se lee. Con una lista de
///    prohibidos, cada extensión nueva del mundo entraría por defecto.
/// </summary>
public static class WorkspacePathPolicy
{
    /// <summary>Un archivo de proyecto legible cabe de sobra aquí; lo que no cabe no es código.</summary>
    public const long MaximumFileBytes = 512 * 1024;

    private static readonly string[] ExcludedDirectories =
    [
        ".git", ".svn", ".hg", "node_modules", "bin", "obj", "dist", "build", "out",
        ".vs", ".vscode", ".idea", "packages", "venv", ".venv", "__pycache__", "target"
    ];

    private static readonly string[] AllowedExtensions =
    [
        ".cs", ".csproj", ".slnx", ".sln", ".props", ".targets",
        ".js", ".ts", ".jsx", ".tsx", ".py", ".java", ".kt", ".go", ".rs", ".rb", ".php",
        ".c", ".h", ".cpp", ".hpp", ".swift", ".sql", ".sh", ".ps1",
        ".json", ".xml", ".yml", ".yaml", ".toml", ".ini", ".config",
        ".md", ".txt", ".html", ".css", ".scss", ".xaml"
    ];

    /// <summary>
    /// Nombres que delatan un archivo de credenciales. Se niegan por NOMBRE, antes de abrirlos:
    /// leer el contenido para decidir si contiene secretos ya sería haberlo leído.
    /// </summary>
    private static readonly string[] SecretFileNames =
    [
        ".env", ".env.local", ".env.production", ".env.development",
        "id_rsa", "id_dsa", "id_ecdsa", "id_ed25519",
        "secrets.json", "credentials", "credentials.json", ".npmrc", ".pypirc", ".netrc"
    ];

    private static readonly string[] SecretExtensions =
    [
        ".pem", ".key", ".pfx", ".p12", ".keystore", ".jks", ".ppk"
    ];

    public static WorkspaceAccessVerdict CanRead(string? candidatePath, string? authorizedRoot)
    {
        if (string.IsNullOrWhiteSpace(authorizedRoot))
        {
            return WorkspaceAccessVerdict.Denied(
                WorkspaceAccessDenial.SinAutorizacion,
                "No tengo ninguna carpeta autorizada. Autoriza una y te ayudo con ese proyecto.");
        }

        if (string.IsNullOrWhiteSpace(candidatePath))
        {
            return WorkspaceAccessVerdict.Denied(
                WorkspaceAccessDenial.FueraDelWorkspace, "No me diste ninguna ruta.");
        }

        if (!IsInside(candidatePath, authorizedRoot))
        {
            return WorkspaceAccessVerdict.Denied(
                WorkspaceAccessDenial.FueraDelWorkspace,
                "Esa ruta está fuera de la carpeta que autorizaste, así que no la leo.");
        }

        var fileName = Path.GetFileName(candidatePath);

        if (IsSecretFile(fileName))
        {
            return WorkspaceAccessVerdict.Denied(
                WorkspaceAccessDenial.ArchivoDeSecretos,
                $"«{fileName}» parece un archivo de credenciales, así que ni lo abro.");
        }

        if (HasExcludedDirectory(candidatePath, authorizedRoot))
        {
            return WorkspaceAccessVerdict.Denied(
                WorkspaceAccessDenial.CarpetaExcluida,
                "Está dentro de una carpeta de dependencias o de compilación, que no es tu código.");
        }

        var extension = Path.GetExtension(candidatePath);
        if (!AllowedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
        {
            return WorkspaceAccessVerdict.Denied(
                WorkspaceAccessDenial.TipoNoSoportado,
                $"No sé leer archivos «{(string.IsNullOrEmpty(extension) ? "sin extensión" : extension)}» como texto de proyecto.");
        }

        return WorkspaceAccessVerdict.Allowed;
    }

    public static WorkspaceAccessVerdict CheckSize(long lengthInBytes) =>
        lengthInBytes > MaximumFileBytes
            ? WorkspaceAccessVerdict.Denied(
                WorkspaceAccessDenial.DemasiadoGrande,
                $"Ese archivo pasa de {MaximumFileBytes / 1024} KB, así que no lo leo entero.")
            : WorkspaceAccessVerdict.Allowed;

    /// <summary>
    /// Contención real: resuelve las dos rutas y compara exigiendo separador. Devuelve false si
    /// alguna no se puede resolver — una ruta que el sistema no sabe interpretar no se lee.
    /// </summary>
    /// <summary>
    /// Corrección de un defecto real: la plantilla que ve el modelo para proponer un cambio
    /// (<c>WorkspaceEditParser.ModelInstructions</c>) usa "/" como separador de ejemplo
    /// ("ruta/relativa/del/archivo"), pero <c>Path.GetRelativePath</c> en Windows produce "\". Una
    /// ruta relativa que viene del texto del modelo y una que viene de recorrer el disco pueden ser
    /// el mismo archivo y no comparar iguales como texto. Esta función resuelve las dos a la misma
    /// forma canónica para que compararlas con <see cref="string.Equals(string?, string?)"/> (o un
    /// <see cref="HashSet{T}"/>) tenga sentido, sin importar qué separador escribió cada lado.
    /// </summary>
    public static string ResolveFullPath(string authorizedRoot, string relativePath) =>
        Path.GetFullPath(Path.Combine(authorizedRoot, NormalizeRelativePath(authorizedRoot, relativePath)));

    /// <summary>
    /// Corrección de un segundo defecto real, encontrado usando la app: el contexto que ve el modelo
    /// empieza por "Proyecto autorizado: &lt;nombre de la carpeta&gt;" y luego lista los archivos
    /// relativos a esa carpeta ("Programa.cs"). Es completamente razonable que el modelo componga las
    /// dos cosas y devuelva "ProyectoPrueba/Programa.cs", y hasta ahora eso se combinaba con la raíz
    /// dando <c>...\ProyectoPrueba\ProyectoPrueba\Programa.cs</c> — un archivo que no existe.
    ///
    /// Las consecuencias no eran cosméticas: la confirmación decía "Voy a CREAR" sobre un archivo que
    /// en realidad ya existía, y la salvaguarda que exige haber leído el contenido actual se salta
    /// entera cuando el archivo "no existe", que es justo la protección añadida tras el borrado del
    /// README. Se quita un único prefijo igual al nombre de la carpeta autorizada; si el proyecto
    /// tiene de verdad una subcarpeta con su mismo nombre, esa ruta sigue alcanzándose nombrándola
    /// dos veces, que es lo que el modelo tendría que escribir para referirse a ella.
    /// </summary>
    public static string NormalizeRelativePath(string authorizedRoot, string? relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return string.Empty;
        }

        var trimmed = relativePath.Trim();
        var rootName = Path.GetFileName(Path.TrimEndingDirectorySeparator(authorizedRoot ?? string.Empty));
        if (string.IsNullOrEmpty(rootName))
        {
            return trimmed;
        }

        var segments = trimmed.Split(
            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
            StringSplitOptions.RemoveEmptyEntries);

        if (segments.Length < 2 ||
            !segments[0].Equals(rootName, StringComparison.OrdinalIgnoreCase))
        {
            return trimmed;
        }

        // Solo se quita si al quitarlo apunta a algo que existe de verdad; si no, se respeta lo que
        // escribió el modelo en vez de adivinar.
        var withoutPrefix = Path.Combine(segments[1..]);
        var candidate = Path.GetFullPath(Path.Combine(authorizedRoot!, withoutPrefix));
        return File.Exists(candidate) ? withoutPrefix : trimmed;
    }

    public static bool IsInside(string? candidatePath, string? authorizedRoot)
    {
        if (string.IsNullOrWhiteSpace(candidatePath) || string.IsNullOrWhiteSpace(authorizedRoot))
        {
            return false;
        }

        string root;
        string candidate;
        try
        {
            root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(authorizedRoot));
            candidate = Path.TrimEndingDirectorySeparator(Path.GetFullPath(candidatePath));
        }
        catch (Exception exception) when (
            exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }

        if (string.Equals(root, candidate, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // El separador es obligatorio: si no, "C:\Proyectos\Sakura" contendría a
        // "C:\Proyectos\Sakura-privado", que es otra carpeta distinta.
        return candidate.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsSecretFile(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return false;
        }

        if (SecretFileNames.Contains(fileName, StringComparer.OrdinalIgnoreCase))
        {
            return true;
        }

        // ".env.staging" y similares: el nombre empieza por .env y sigue con lo que sea.
        if (fileName.StartsWith(".env", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return SecretExtensions.Contains(Path.GetExtension(fileName), StringComparer.OrdinalIgnoreCase);
    }

    public static bool IsExcludedDirectoryName(string? name) =>
        !string.IsNullOrWhiteSpace(name) &&
        ExcludedDirectories.Contains(name, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Comprueba los segmentos ENTRE la raíz autorizada y el archivo. La raíz misma no se examina a
    /// propósito: si alguien autoriza una carpeta llamada "build", su proyecto se llama build y
    /// negarse a leerlo entero sería absurdo.
    /// </summary>
    private static bool HasExcludedDirectory(string candidatePath, string authorizedRoot)
    {
        string root;
        string candidate;
        try
        {
            root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(authorizedRoot));
            candidate = Path.GetFullPath(candidatePath);
        }
        catch (Exception exception) when (
            exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return true;
        }

        var relative = Path.GetRelativePath(root, candidate);
        var segments = relative.Split(
            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
            StringSplitOptions.RemoveEmptyEntries);

        // El último segmento es el archivo, no una carpeta.
        return segments.Take(Math.Max(0, segments.Length - 1)).Any(IsExcludedDirectoryName);
    }
}
