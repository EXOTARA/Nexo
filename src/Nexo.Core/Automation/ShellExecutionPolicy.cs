namespace Nexo.Core.Automation;

/// <summary>
/// Decide si una acción de "abrir una aplicación" es en realidad **ejecución de un comando
/// arbitrario**, y por tanto necesita confirmación explícita del usuario.
///
/// <para>
/// <b>Por qué existe (defecto D2 de la fase 1.1).</b> <c>OpenApplication</c> reenvía
/// <see cref="AutomationAction.Arguments"/> al proceso y estaba clasificada como
/// <see cref="AutomationRiskLevel.Reversible"/>, es decir, **sin confirmación**. Un paso de
/// rutina con <c>Target="powershell.exe"</c> y <c>Arguments="-Command ..."</c> era ejecución
/// arbitraria sin aprobación, lo que incumple `SECURITY_MODEL` (escenario 22 de
/// `TEST_MATRIX`) y la decisión F de `PRODUCT_VISION`.
/// </para>
///
/// <para><b>Reglas normativas implementadas:</b></para>
/// <list type="bullet">
///   <item>Abrir un intérprete <b>sin argumentos</b> no requiere confirmación: abrir la
///     terminal no es ejecutar en ella.</item>
///   <item>Un intérprete <b>con cualquier argumento</b> requiere confirmación. No se intenta
///     distinguir banderas "inocuas" de banderas de ejecución: una lista blanca de banderas es
///     exactamente el tipo de defensa frágil que se evita. Mínimo privilegio por defecto.</item>
///   <item>La detección **no se basa solo en el nombre visible**: se normaliza ruta completa,
///     comillas, variables de entorno, separadores, mayúsculas, extensión omitida y los
///     puntos y espacios finales que Windows ignora al resolver un ejecutable.</item>
///   <item>Los **argumentos también se inspeccionan**: invocar un intérprete desde los
///     argumentos de otro programa (p. ej. <c>explorer.exe</c> con
///     <c>"powershell -Command ..."</c>) también requiere confirmación.</item>
/// </list>
///
/// <para>
/// <b>Alcance.</b> Esta política evalúa la **acción**, nunca quién la pidió. No existe ninguna
/// marca de "ya aprobado" que pueda saltársela, así que una rutina aprobada al crearse no
/// hereda permiso para ejecutar comandos nuevos, y el contenido que llegue del modelo, OCR,
/// archivos, web o skills no puede adquirir autoridad de usuario por el mero hecho de viajar
/// dentro de una acción. La identidad del actor como concepto de primera clase llega en la
/// Fase 5 (`SECURITY_MODEL` §Defensa por capas, punto 2).
/// </para>
/// </summary>
public static class ShellExecutionPolicy
{
    /// <summary>
    /// Intérpretes de comandos, hosts de scripts y binarios del sistema que se usan
    /// habitualmente para ejecutar código arbitrario. La lista es deliberadamente amplia:
    /// un falso positivo solo añade una confirmación; un falso negativo es ejecución sin
    /// aprobación.
    /// </summary>
    private static readonly HashSet<string> Interpreters = new(StringComparer.OrdinalIgnoreCase)
    {
        // Shells de Windows
        "powershell",
        "powershell_ise",
        "pwsh",
        "cmd",
        "command",
        // Hosts de scripts
        "wscript",
        "cscript",
        "mshta",
        // Binarios del sistema que ejecutan código ajeno
        "rundll32",
        "regsvr32",
        "installutil",
        "msbuild",
        "wmic",
        // Shells y runtimes de origen Unix disponibles en Windows
        "wsl",
        "bash",
        "sh",
        "zsh",
        "python",
        "pythonw",
        "node",
        "ruby",
        "perl",
        "php",
        "dotnet"
    };

    /// <summary>
    /// ¿El ejecutable indicado es un intérprete capaz de ejecutar código arbitrario?
    /// </summary>
    public static bool IsInterpreter(string? target) =>
        Interpreters.Contains(NormalizeExecutableName(target));

    /// <summary>
    /// ¿Esta combinación de ejecutable y argumentos constituye ejecución de un comando
    /// arbitrario y, por tanto, exige confirmación explícita?
    /// </summary>
    public static bool RequiresConfirmation(string? target, string? arguments)
    {
        // Un intérprete con cualquier argumento deja de ser "abrir" y pasa a ser "ejecutar".
        if (IsInterpreter(target) && !string.IsNullOrWhiteSpace(arguments))
        {
            return true;
        }

        // Un programa cualquiera que invoque un intérprete desde sus argumentos.
        return MentionsInterpreter(arguments);
    }

    /// <summary>
    /// ¿Los argumentos nombran un intérprete? Cubre el rodeo de lanzar un shell a través de
    /// otro programa.
    /// </summary>
    public static bool MentionsInterpreter(string? arguments)
    {
        if (string.IsNullOrWhiteSpace(arguments))
        {
            return false;
        }

        foreach (var token in Tokenize(arguments))
        {
            if (Interpreters.Contains(NormalizeExecutableName(token)))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Reduce cualquier forma de referirse a un ejecutable a su nombre base en minúsculas.
    /// Maneja comillas, rutas completas, variables de entorno, ambos separadores, extensión
    /// presente o ausente, y los espacios y puntos finales que Windows descarta al resolver
    /// un nombre de archivo.
    /// </summary>
    public static string NormalizeExecutableName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var text = value.Trim().Trim('"', '\'');

        // Windows ignora espacios y puntos finales: "powershell.exe. " resuelve igual.
        text = text.TrimEnd(' ', '.');

        // Último segmento de la ruta, sea cual sea el separador. Cubre rutas absolutas y
        // rutas con variables de entorno sin necesidad de expandirlas.
        var separator = text.LastIndexOfAny(['\\', '/']);
        if (separator >= 0)
        {
            text = text[(separator + 1)..];
        }

        text = text.TrimEnd(' ', '.');

        // Extensión ejecutable opcional.
        foreach (var extension in new[] { ".exe", ".com", ".bat", ".cmd", ".ps1" })
        {
            if (text.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
            {
                text = text[..^extension.Length];
                break;
            }
        }

        return text.Trim();
    }

    /// <summary>
    /// Separa una línea de argumentos respetando comillas, para no partir rutas con espacios.
    /// </summary>
    private static IEnumerable<string> Tokenize(string arguments)
    {
        var current = new System.Text.StringBuilder();
        var quote = '\0';

        foreach (var character in arguments)
        {
            if (quote != '\0')
            {
                if (character == quote)
                {
                    quote = '\0';
                }
                else
                {
                    current.Append(character);
                }

                continue;
            }

            if (character is '"' or '\'')
            {
                quote = character;
                continue;
            }

            if (char.IsWhiteSpace(character))
            {
                if (current.Length > 0)
                {
                    yield return current.ToString();
                    current.Clear();
                }

                continue;
            }

            current.Append(character);
        }

        if (current.Length > 0)
        {
            yield return current.ToString();
        }
    }
}
