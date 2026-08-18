using Nexo.Core.Ai;
using Nexo.Core.Settings;

namespace Nexo.Core.Tests;

/// <summary>
/// Diseño D58 — el modelo se recuerda por proveedor, como ya se hacía con las claves.
///
/// Cambiar de proveedor pisaba el modelo con el del preset, así que ir a Gemini y volver a Groq no
/// devolvía la elección de la persona sino la de fábrica. Quien tenía el modelo afinado lo perdía
/// en cada ida y vuelta.
/// </summary>
public sealed class AiModelMemoryTests
{
    [Fact]
    public void UpgradingRemembersTheModelYouAlreadyHad()
    {
        // Migrar con la memoria vacía perdería la elección en el primer cambio de proveedor, que
        // es justo cuando se notaría.
        var preferences = new ShellPreferences
        {
            SchemaVersion = 25,
            AiProvider = AiProviderKind.Groq,
            AiModel = "llama-3.1-8b-instant"
        };

        preferences.Normalize();

        Assert.Equal(
            "llama-3.1-8b-instant",
            preferences.AiModelsByProvider[AiProviderKind.Groq.ToString()]);
    }

    [Fact]
    public void WithNoProvider_NothingIsRemembered()
    {
        var preferences = new ShellPreferences
        {
            SchemaVersion = 25,
            AiProvider = AiProviderKind.Disabled,
            AiModel = "algo"
        };

        preferences.Normalize();

        Assert.Empty(preferences.AiModelsByProvider);
    }

    [Fact]
    public void TheKeyIsTheProviderName_NotItsNumber()
    {
        // Los números de un enum se reordenan al añadir uno en medio, y eso convertiría el modelo
        // de un proveedor en el de otro sin que nadie tocara nada.
        var preferences = new ShellPreferences
        {
            SchemaVersion = 25,
            AiProvider = AiProviderKind.Gemini,
            AiModel = "gemini-flash-latest"
        };

        preferences.Normalize();

        Assert.True(preferences.AiModelsByProvider.ContainsKey("Gemini"));
    }

    [Fact]
    public void TheMemorySurvivesAFullNormalize()
    {
        var preferences = new ShellPreferences
        {
            SchemaVersion = ShellPreferences.CurrentSchemaVersion,
            AiProvider = AiProviderKind.Groq,
            AiModel = "llama-3.3-70b-versatile",
            AiModelsByProvider = new Dictionary<string, string>
            {
                ["Gemini"] = "gemini-flash-latest"
            }
        };

        preferences.Normalize();

        Assert.Equal("gemini-flash-latest", preferences.AiModelsByProvider["Gemini"]);
    }
}
