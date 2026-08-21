using System.Globalization;

namespace Nexo.Core.Updates;

/// <summary>
/// Diseño D62 — una versión de Sakura, y cuál de dos es más nueva.
///
/// Existe porque comparar versiones «a ojo» es donde viven los errores caros de un actualizador.
/// El clásico es comparar como texto: así «0.9.10» es menor que «0.9.5», porque el 1 va antes que
/// el 5 — y un actualizador que se cree eso deja de ofrecer actualizaciones justo cuando empiezan a
/// importar, sin dar ninguna señal de que algo va mal.
///
/// Se siguen las reglas de versionado semántico en las tres cosas que aquí importan:
///
/// **Los números se comparan como números**, no como letras.
///
/// **Una versión final es más nueva que su propia preliminar**: 0.9.5 gana a 0.9.5-beta. Es al
/// revés de lo que sugiere la longitud del texto, y es la regla que hace que salir de beta cuente
/// como actualización.
///
/// **Lo que va tras el «+» no cuenta para nada.** La versión informativa que genera la compilación
/// es «0.9.5-beta+563688b…», con el identificador del commit pegado. Si eso contara, cada
/// compilación parecería una versión distinta y Sakura se ofrecería actualizarse a sí misma.
/// </summary>
public readonly record struct SakuraVersion(
    int Major,
    int Minor,
    int Patch,
    string PreRelease) : IComparable<SakuraVersion>
{
    public bool IsPreRelease => PreRelease.Length > 0;

    /// <summary>
    /// Lee una versión. Devuelve falso en vez de lanzar: esto se usa sobre texto que llega de fuera
    /// —el archivo que anuncia la versión publicada— y un servidor que responde cualquier cosa es un
    /// caso normal, no una excepción.
    /// </summary>
    public static bool TryParse(string? text, out SakuraVersion version)
    {
        version = default;

        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var value = text.Trim();

        // Algunas etiquetas de publicación llegan como «v1.2.3».
        if (value.StartsWith('v') || value.StartsWith('V'))
        {
            value = value[1..];
        }

        // Los metadatos de compilación se descartan antes de nada: no participan en el orden.
        var plus = value.IndexOf('+');
        if (plus >= 0)
        {
            value = value[..plus];
        }

        var preRelease = string.Empty;
        var dash = value.IndexOf('-');
        if (dash >= 0)
        {
            preRelease = value[(dash + 1)..].Trim();
            value = value[..dash];
        }

        var parts = value.Split('.');
        if (parts.Length is < 1 or > 4)
        {
            return false;
        }

        // Se admiten tres o cuatro números porque Windows escribe cuatro («0.9.5.0») y el proyecto
        // publica tres. El cuarto se ignora: no forma parte del orden semántico y tratarlo como
        // significativo haría que «0.9.5.0» y «0.9.5» parecieran distintas.
        var numbers = new int[3];
        for (var i = 0; i < 3; i++)
        {
            if (i >= parts.Length)
            {
                numbers[i] = 0;
                continue;
            }

            if (!int.TryParse(parts[i], NumberStyles.None, CultureInfo.InvariantCulture, out var number))
            {
                return false;
            }

            numbers[i] = number;
        }

        version = new SakuraVersion(numbers[0], numbers[1], numbers[2], preRelease);
        return true;
    }

    public int CompareTo(SakuraVersion other)
    {
        var byNumbers =
            Major.CompareTo(other.Major) is var major and not 0 ? major :
            Minor.CompareTo(other.Minor) is var minor and not 0 ? minor :
            Patch.CompareTo(other.Patch);

        return byNumbers != 0 ? byNumbers : ComparePreRelease(PreRelease, other.PreRelease);
    }

    /// <summary>
    /// Ordena las etiquetas preliminares. Sin etiqueta gana a con etiqueta —una final es posterior
    /// a su preliminar—, y entre dos etiquetas se comparan sus partes separadas por puntos, con los
    /// números como números para que «beta.10» vaya después de «beta.9».
    /// </summary>
    private static int ComparePreRelease(string left, string right)
    {
        if (left.Length == 0 && right.Length == 0)
        {
            return 0;
        }

        if (left.Length == 0)
        {
            return 1;
        }

        if (right.Length == 0)
        {
            return -1;
        }

        var leftParts = left.Split('.');
        var rightParts = right.Split('.');

        for (var i = 0; i < Math.Max(leftParts.Length, rightParts.Length); i++)
        {
            // Quien se queda sin partes va antes: «beta» precede a «beta.1».
            if (i >= leftParts.Length)
            {
                return -1;
            }

            if (i >= rightParts.Length)
            {
                return 1;
            }

            var leftIsNumber = int.TryParse(
                leftParts[i], NumberStyles.None, CultureInfo.InvariantCulture, out var leftNumber);
            var rightIsNumber = int.TryParse(
                rightParts[i], NumberStyles.None, CultureInfo.InvariantCulture, out var rightNumber);

            if (leftIsNumber && rightIsNumber)
            {
                if (leftNumber != rightNumber)
                {
                    return leftNumber.CompareTo(rightNumber);
                }

                continue;
            }

            // Una parte numérica va antes que una de texto, como manda el versionado semántico.
            if (leftIsNumber != rightIsNumber)
            {
                return leftIsNumber ? -1 : 1;
            }

            var byText = string.CompareOrdinal(leftParts[i], rightParts[i]);
            if (byText != 0)
            {
                return byText;
            }
        }

        return 0;
    }

    public static bool operator <(SakuraVersion left, SakuraVersion right) => left.CompareTo(right) < 0;

    public static bool operator >(SakuraVersion left, SakuraVersion right) => left.CompareTo(right) > 0;

    public static bool operator <=(SakuraVersion left, SakuraVersion right) => left.CompareTo(right) <= 0;

    public static bool operator >=(SakuraVersion left, SakuraVersion right) => left.CompareTo(right) >= 0;

    public override string ToString() =>
        IsPreRelease
            ? $"{Major}.{Minor}.{Patch}-{PreRelease}"
            : $"{Major}.{Minor}.{Patch}";
}
