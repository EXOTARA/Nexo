using System.IO;
using System.Text.RegularExpressions;
using Xunit;

namespace Nexo.Windows.Tests.Design;

/// <summary>
/// Diseño D65 — todo control con el que se pueda interactuar tiene que decir su nombre.
///
/// Un lector de pantalla no ve iconos. Un botón cuyo contenido es un dibujo y cuyo único texto está
/// en una descripción emergente se anuncia como "botón", sin más; una caja de texto sin nombre se
/// anuncia como "editar". Recorriendo el árbol de accesibilidad de Kohana en marcha aparecieron
/// cinco así en la pantalla principal, y uno era la caja donde se escribe todo.
///
/// La comprobación se hace sobre el XAML y no sobre la aplicación en marcha a propósito: así cubre
/// también las vistas que no están cargadas —que son casi todas en cualquier momento dado— y falla
/// en cuanto alguien añade un control nuevo sin nombre, que es cuando cuesta barato arreglarlo.
///
/// Quedan fuera las plantillas de <c>Themes/</c>: sus piezas internas (los botones de una barra de
/// desplazamiento, el interruptor de un desplegable) las nombra WPF por su cuenta según el control
/// al que sirven, y ponerles un nombre fijo aquí las haría mentir en la mitad de los sitios.
/// </summary>
public sealed class AccessibleNameTests
{
    private static readonly string RepositoryRoot = FindRepositoryRoot();

    private static readonly string[] InteractiveElements =
    [
        "Button", "ToggleButton", "RepeatButton", "TextBox", "PasswordBox", "ComboBox", "Slider"
    ];

    [Fact]
    public void EveryInteractiveControlInTheViews_SaysWhatItIs()
    {
        var offenders = new List<string>();
        var viewsDirectory = Path.Combine(RepositoryRoot, "src", "Nexo.App");

        foreach (var file in Directory.EnumerateFiles(viewsDirectory, "*.xaml", SearchOption.AllDirectories))
        {
            // Las plantillas viven en Themes/ y las nombra WPF; ver el resumen de la clase.
            if (file.Contains($"{Path.DirectorySeparatorChar}Themes{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            {
                continue;
            }

            var content = File.ReadAllText(file);
            // El lookahead evita que "<ComboBox.ItemTemplate>" cuente como un ComboBox: en XAML una
            // propiedad adjunta se escribe igual que el elemento hasta el punto.
            var pattern = $@"<({string.Join('|', InteractiveElements)})(?=[\s/>])((?:[^<>]|""[^""]*"")*?)>";

            foreach (Match match in Regex.Matches(content, pattern, RegexOptions.Singleline))
            {
                var attributes = match.Groups[2].Value;

                // Un control con texto propio ya se anuncia con ese texto.
                if (attributes.Contains("AutomationProperties.Name", StringComparison.Ordinal) ||
                    attributes.Contains("Content=\"", StringComparison.Ordinal))
                {
                    continue;
                }

                var line = content[..match.Index].Count(c => c == '\n') + 1;
                var name = Regex.Match(attributes, @"x:Name=""([^""]+)""");
                offenders.Add($"{Path.GetFileName(file)}:{line} {match.Groups[1].Value} {(name.Success ? name.Groups[1].Value : "(sin nombre)")}");
            }
        }

        Assert.True(
            offenders.Count == 0,
            "Controles sin nombre accesible:" + Environment.NewLine + string.Join(Environment.NewLine, offenders));
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Nexo.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new InvalidOperationException("No se encontró Nexo.slnx desde el directorio de pruebas.");
    }
}
