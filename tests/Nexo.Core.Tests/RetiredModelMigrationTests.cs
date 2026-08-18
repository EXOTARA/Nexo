using Nexo.Core.Ai;
using Nexo.Core.Media;
using Nexo.Core.Settings;

namespace Nexo.Core.Tests;

/// <summary>
/// Diseño D60 — un modelo que el proveedor apaga deja de estar guardado.
///
/// Adler lo vio con Groq: «The model llama-3.3-70b-versatile does not exist or you do not have
/// access to it». Groq lo retiró el 17 de junio de 2026 para las capas gratuita y de desarrollador,
/// que son justo las que usa quien viene por «gratis y sin tarjeta». El mensaje habla de accesos,
/// así que parece que la clave está mal — y no lo está.
/// </summary>
public sealed class RetiredModelMigrationTests
{
    [Fact]
    public void TheRetiredGroqModel_IsSwappedForItsReplacement()
    {
        var preferences = new ShellPreferences
        {
            SchemaVersion = 27,
            AiProvider = AiProviderKind.Groq,
            AiModel = "llama-3.3-70b-versatile"
        };

        preferences.Normalize();

        Assert.Equal("openai/gpt-oss-120b", preferences.AiModel);
    }

    [Fact]
    public void ItIsAlsoSwappedInThePerProviderMemory()
    {
        // Cambiar solo el modelo activo dejaría el muerto esperando a que se vuelva al proveedor.
        var preferences = new ShellPreferences
        {
            SchemaVersion = 27,
            AiProvider = AiProviderKind.Gemini,
            AiModel = "gemini-flash-latest",
            AiModelsByProvider = new Dictionary<string, string>
            {
                ["Groq"] = "llama-3.3-70b-versatile"
            }
        };

        preferences.Normalize();

        Assert.Equal("openai/gpt-oss-120b", preferences.AiModelsByProvider["Groq"]);
        Assert.Equal("gemini-flash-latest", preferences.AiModel);
    }

    [Fact]
    public void AModelThatStillWorks_IsLeftAlone()
    {
        var preferences = new ShellPreferences
        {
            SchemaVersion = 27,
            AiProvider = AiProviderKind.Groq,
            AiModel = "llama-3.1-8b-instant"
        };

        preferences.Normalize();

        Assert.Equal("llama-3.1-8b-instant", preferences.AiModel);
    }

    [Fact]
    public void TheGroqPresetNoLongerHandsOutTheDeadModel()
    {
        // El valor por omisión y lo ya guardado son dos caminos distintos hacia el mismo fallo, y
        // hay que cerrar los dos.
        Assert.NotEqual(
            "llama-3.3-70b-versatile",
            AiProviderDefaults.Get(AiProviderKind.Groq).DefaultModel);
    }
}

/// <summary>
/// Diseño D60 — de dónde sale la música, cuando Windows no lo dice con un nombre.
/// </summary>
public sealed class OpaqueMediaSourceTests
{
    [Fact]
    public void AnOpaqueWindowsIdentifier_IsNotShown()
    {
        // Adler vio «F0dc299d809b9700» bajo la carátula. Windows le inventa ese identificador a las
        // aplicaciones que no declaran el suyo, y un número ahí confunde más que un hueco vacío
        // porque parece que significa algo.
        Assert.Equal(string.Empty, MediaSourceName.Describe("F0dc299d809b9700"));
    }

    [Fact]
    public void RealNames_StillCome()
    {
        Assert.Equal("Spotify", MediaSourceName.Describe("Spotify.exe"));
        Assert.Equal("Google Chrome", MediaSourceName.Describe("chrome.exe"));
    }

    [Fact]
    public void AShortHexLikeWord_IsNotMistakenForAnIdentifier()
    {
        // «faded» son cinco letras hexadecimales y es una palabra. El largo forma parte de la
        // prueba, no solo la forma.
        Assert.Equal("Faded", MediaSourceName.Describe("faded.exe"));
    }
}
