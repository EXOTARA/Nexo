using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Nexo.Windows.Tests.Design;

/// <summary>
/// Invariantes estructurales del Design System (Fundación 0.1). No pueden expresarse contra la
/// API porque el tema vive en <c>Nexo.App</c> (arrastra <c>UseWPF</c>), así que se leen los
/// diccionarios XAML como XML/texto: recursos cargados, claves únicas, recursos semánticos
/// obligatorios presentes, ausencia de referencias de token rotas, y que App fusione el
/// agregador y los controles principales conserven sus estilos.
/// </summary>
public sealed class DesignSystemResourceTests
{
    private static readonly string RepositoryRoot = FindRepositoryRoot();
    private static readonly XNamespace Xaml = "http://schemas.microsoft.com/winfx/2006/xaml";

    private static string ThemePath(string file) =>
        Path.Combine(RepositoryRoot, "src", "Nexo.App", "Themes", file);

    private static readonly string[] ThemeFiles =
    [
        "Colors.xaml", "Typography.xaml", "Spacing.xaml", "Motion.xaml",
        "Brushes.xaml", "Brand.xaml", "Controls.xaml", "ThemeResources.xaml"
    ];

    [Fact]
    public void EveryThemeDictionary_ExistsAndIsWellFormedXml()
    {
        foreach (var file in ThemeFiles)
        {
            var path = ThemePath(file);
            Assert.True(File.Exists(path), $"Falta el diccionario de tema {file}.");
            var exception = Record.Exception(() => XDocument.Load(path));
            Assert.Null(exception);
        }
    }

    [Fact]
    public void App_MergesOnlyTheThemeResourcesAggregator()
    {
        var app = File.ReadAllText(Path.Combine(RepositoryRoot, "src", "Nexo.App", "App.xaml"));

        Assert.Contains("Themes/ThemeResources.xaml", app, StringComparison.Ordinal);
    }

    [Fact]
    public void ThemeResources_MergesEveryFoundationDictionaryInDependencyOrder()
    {
        var content = File.ReadAllText(ThemePath("ThemeResources.xaml"));

        // Colors debe fusionarse antes que Brushes (que referencia sus Color*), Brand y Controls.
        var colorsIndex = content.IndexOf("Colors.xaml", StringComparison.Ordinal);
        var brushesIndex = content.IndexOf("Brushes.xaml", StringComparison.Ordinal);
        var controlsIndex = content.IndexOf("Controls.xaml", StringComparison.Ordinal);

        foreach (var file in new[] { "Colors.xaml", "Typography.xaml", "Spacing.xaml", "Motion.xaml", "Brushes.xaml", "Brand.xaml", "Controls.xaml" })
        {
            Assert.Contains(file, content, StringComparison.Ordinal);
        }

        Assert.True(colorsIndex >= 0 && colorsIndex < brushesIndex,
            "Colors.xaml debe fusionarse antes que Brushes.xaml.");
        Assert.True(brushesIndex < controlsIndex,
            "Brushes.xaml debe fusionarse antes que Controls.xaml.");
    }

    [Fact]
    public void RequiredSemanticResources_AreDefined()
    {
        var defined = CollectDefinedKeys();

        string[] required =
        [
            // Superficies y texto
            "BrushBackground", "BrushSurface", "BrushSurfaceRaised", "BrushSurfaceHover",
            "BrushTextPrimary", "BrushTextSecondary", "BrushTextMuted",
            // Acento y bordes
            "BrushAccent", "BrushAccentSoft", "BrushBorder", "BrushFocusRing",
            // Estados
            "BrushSuccess", "BrushWarning", "BrushError",
            // Escalas de fundación
            "FontFamilyPrimary", "FontSizeBody", "RadiusMedium", "Space12", "MotionBase"
        ];

        foreach (var key in required)
        {
            Assert.Contains(key, defined);
        }
    }

    [Fact]
    public void EachDictionary_HasNoDuplicateKeys()
    {
        // WPF exige claves únicas dentro de un mismo ResourceDictionary.
        foreach (var file in ThemeFiles)
        {
            var keys = KeysDefinedIn(ThemePath(file));
            var duplicates = keys.GroupBy(k => k).Where(g => g.Count() > 1).Select(g => g.Key).ToArray();
            Assert.True(duplicates.Length == 0, $"{file} tiene claves duplicadas: {string.Join(", ", duplicates)}.");
        }
    }

    [Fact]
    public void FoundationTokenReferences_AreNotBroken()
    {
        // Todo {Static/DynamicResource X} cuya clave empiece por un prefijo de token de la
        // fundación debe estar definido en algún diccionario del tema. Así se detecta un token
        // mal escrito o eliminado sin tener que ejecutar WPF.
        var defined = CollectDefinedKeys();
        string[] tokenPrefixes = ["Brush", "Color", "Radius", "FontSize", "FontFamily", "Space", "Motion"];

        var referenceRegex = new Regex(@"\{(?:Static|Dynamic)Resource\s+([A-Za-z0-9_]+)\}");
        var missing = new List<string>();

        foreach (var file in ThemeFiles)
        {
            var text = File.ReadAllText(ThemePath(file));
            foreach (Match match in referenceRegex.Matches(text))
            {
                var key = match.Groups[1].Value;
                if (tokenPrefixes.Any(p => key.StartsWith(p, StringComparison.Ordinal)) && !defined.Contains(key))
                {
                    missing.Add($"{file}: {key}");
                }
            }
        }

        Assert.True(missing.Count == 0, $"Referencias de token rotas: {string.Join("; ", missing)}.");
    }

    [Fact]
    public void MainControlStyles_ArePreserved()
    {
        var controls = File.ReadAllText(ThemePath("Controls.xaml"));

        string[] mainStyles =
        [
            "PrimaryButtonStyle", "SecondaryButtonStyle", "NavButtonStyle", "IconButtonStyle",
            "CardStyle", "SubtleCardStyle", "PromptTextBoxStyle", "SectionTitleStyle"
        ];

        foreach (var style in mainStyles)
        {
            Assert.Contains($"x:Key=\"{style}\"", controls, StringComparison.Ordinal);
        }
    }

    private static HashSet<string> CollectDefinedKeys()
    {
        var all = new HashSet<string>(StringComparer.Ordinal);
        foreach (var file in ThemeFiles)
        {
            foreach (var key in KeysDefinedIn(ThemePath(file)))
            {
                all.Add(key);
            }
        }

        return all;
    }

    private static List<string> KeysDefinedIn(string path)
    {
        var document = XDocument.Load(path);
        return document.Descendants()
            .Select(element => element.Attribute(Xaml + "Key")?.Value)
            .Where(value => !string.IsNullOrEmpty(value))
            .Select(value => value!)
            .ToList();
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
