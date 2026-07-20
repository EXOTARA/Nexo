using Nexo.Core.Commands;

namespace Nexo.Core.Tests;

public sealed class CommandPaletteInputPolicyTests
{
    private static readonly string[] ExplorerKeywords =
        ["e", "explorador", "archivos", "carpetas"];

    [Fact]
    public void NaturalPrompt_IsDetected()
    {
        Assert.True(CommandPaletteInputPolicy.IsLikelyNaturalPrompt(
            "Dame una receta de profiteroles detallada y ponmela en una nota"));
    }

    [Fact]
    public void ShortCommand_IsNotTreatedAsNaturalPrompt()
    {
        Assert.False(CommandPaletteInputPolicy.IsLikelyNaturalPrompt("explorador"));
    }

    [Theory]
    [InlineData("Explorador de archivos")]
    [InlineData("abre el explorador de archivos")]
    [InlineData("explorador")]
    [InlineData("e")]
    public void ExactSuggestionVariants_AreRecognized(string query)
    {
        Assert.True(CommandPaletteInputPolicy.IsExactSuggestionMatch(
            query,
            "Explorador de archivos",
            "abre el explorador de archivos",
            ExplorerKeywords));
    }

    [Fact]
    public void NaturalPrompt_DoesNotExecuteSelectedSuggestion()
    {
        Assert.False(CommandPaletteInputPolicy.ShouldExecuteSuggestion(
            "Dame una receta de profiteroles detallada y ponmela en una nota",
            "Pendientes de hoy",
            "¿Qué tengo pendiente hoy?",
            ["pendientes", "hoy", "tareas"],
            score: 60,
            completionActive: false,
            selectionExplicit: false));
    }

    [Fact]
    public void ActiveCompletion_ExecutesSuggestion()
    {
        Assert.True(CommandPaletteInputPolicy.ShouldExecuteSuggestion(
            "Explorador de archivos",
            "Explorador de archivos",
            "abre el explorador de archivos",
            ExplorerKeywords,
            score: 220,
            completionActive: true,
            selectionExplicit: false));
    }

    [Fact]
    public void ExplicitKeyboardSelection_ExecutesSuggestion()
    {
        Assert.True(CommandPaletteInputPolicy.ShouldExecuteSuggestion(
            "dame algo",
            "Descargas",
            "abre descargas",
            ["d", "descargas"],
            score: 40,
            completionActive: false,
            selectionExplicit: true));
    }
}
