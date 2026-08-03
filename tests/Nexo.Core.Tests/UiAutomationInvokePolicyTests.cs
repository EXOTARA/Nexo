using Nexo.Core.ComputerUse;
using Nexo.Core.Vision;

namespace Nexo.Core.Tests;

/// <summary>
/// Diseño D19 (Fase 7, escalón 5) — la regla de esta clase es "ante la duda, no se pulsa", así que
/// casi todo lo que se prueba aquí es cuándo se niega a pulsar.
/// </summary>
public sealed class UiAutomationInvokePolicyTests
{
    private static UiAutomationElement Element(string name, string controlType = "ControlType.Button") =>
        new(name, controlType, 0, 0, 80, 24);

    private static UiAutomationSnapshot Window(params UiAutomationElement[] elements) =>
        UiAutomationSnapshot.Success("Ventana de prueba", elements, 0, 0, 800, 600);

    [Fact]
    public void AUniqueButton_CanBeInvoked()
    {
        var verdict = UiAutomationInvokePolicy.Decide(
            Window(Element("Aceptar"), Element("Cancelar")), "Aceptar");

        Assert.True(verdict.IsAllowed);
        Assert.Equal("Aceptar", verdict.Target!.Name);
    }

    [Fact]
    public void TheNameIsMatchedWithoutCaring_AboutCaseOrSurroundingSpaces()
    {
        Assert.True(UiAutomationInvokePolicy.Decide(Window(Element("  Aceptar ")), "aceptar").IsAllowed);
    }

    [Fact]
    public void SeveralControlsWithTheSameName_MeanNoneIsPressed()
    {
        // Es el caso importante: una ventana con tres "Aceptar" no tiene un "el Aceptar", y elegir
        // el primero del árbol es elegir al azar con apariencia de criterio.
        var verdict = UiAutomationInvokePolicy.Decide(
            Window(Element("Aceptar"), Element("Aceptar"), Element("Aceptar")), "Aceptar");

        Assert.Equal(InvokeRefusal.Ambiguo, verdict.Refusal);
        Assert.Contains("3 controles", verdict.Message);
    }

    [Fact]
    public void NoMatch_MeansNothingIsApproximated()
    {
        // Aproximar un nombre es pulsar un botón que nadie pidió.
        var verdict = UiAutomationInvokePolicy.Decide(Window(Element("Aceptar")), "Acept");

        Assert.Equal(InvokeRefusal.NoEncontrado, verdict.Refusal);
    }

    [Theory]
    [InlineData("ControlType.Text")]
    [InlineData("ControlType.Edit")]
    [InlineData("ControlType.ComboBox")]
    [InlineData("ControlType.Inventado")]
    public void ControlsThatAreNotPressed_AreNotPressed(string controlType)
    {
        // Lista de permitidos: lo que no se reconoce no se pulsa.
        var verdict = UiAutomationInvokePolicy.Decide(
            Window(Element("Algo", controlType)), "Algo");

        Assert.Equal(InvokeRefusal.NoInvocable, verdict.Refusal);
    }

    [Theory]
    [InlineData("ControlType.Button")]
    [InlineData("ControlType.MenuItem")]
    [InlineData("ControlType.Hyperlink")]
    [InlineData("ControlType.CheckBox")]
    public void ControlsThatArePressed_CanBeInvoked(string controlType) =>
        Assert.True(UiAutomationInvokePolicy.Decide(Window(Element("Algo", controlType)), "Algo").IsAllowed);

    [Fact]
    public void AWindowThatCouldNotBeRead_IsNotTouched()
    {
        var verdict = UiAutomationInvokePolicy.Decide(
            UiAutomationSnapshot.Failed("No se pudo leer."), "Aceptar");

        Assert.Equal(InvokeRefusal.SinLectura, verdict.Refusal);
    }

    [Fact]
    public void WithNoControlName_NothingIsPressed()
    {
        Assert.Equal(
            InvokeRefusal.NoEncontrado,
            UiAutomationInvokePolicy.Decide(Window(Element("Aceptar")), null).Refusal);
    }

    [Fact]
    public void ASensitiveWindow_IsNotTouched()
    {
        // Si Kohana no puede ni mirar un gestor de contraseñas, mucho menos pulsar dentro de él.
        var verdict = UiAutomationInvokePolicy.CheckWindow("Bitwarden", "bitwarden");

        Assert.Equal(InvokeRefusal.VentanaSensible, verdict.Refusal);
    }

    [Fact]
    public void AnOrdinaryWindow_Passes() =>
        Assert.True(UiAutomationInvokePolicy.CheckWindow("Bloc de notas", "notepad").IsAllowed);

    // ---------- Objetivos ----------

    /// <summary>
    /// Diseño D19 — el conjunto sobre el que se aplica el orden estricto. Pulsar admite toda la
    /// escalera; copiar, solo el portapapeles.
    /// </summary>
    [Fact]
    public void EachGoalKnowsWhichMethodsCouldAchieveIt()
    {
        var pressing = ComputerUseGoals.CapableMethods(ComputerUseGoal.PulsarControl);
        Assert.Contains(ComputerUseMethod.UiAutomation, pressing);
        Assert.Contains(ComputerUseMethod.RatonTeclado, pressing);
        Assert.DoesNotContain(ComputerUseMethod.Portapapeles, pressing);

        var copying = ComputerUseGoals.CapableMethods(ComputerUseGoal.CopiarTexto);
        Assert.Equal([ComputerUseMethod.Portapapeles], copying);
    }

    [Fact]
    public void CapableMethodsAreListedSafestFirst()
    {
        var pressing = ComputerUseGoals.CapableMethods(ComputerUseGoal.PulsarControl);

        Assert.Equal(pressing.OrderBy(method => (int)method), pressing);
    }

    [Fact]
    public void EveryImplementedMethodBelongsToAGoal()
    {
        foreach (var method in new[]
                 {
                     ComputerUseMethod.UiAutomation,
                     ComputerUseMethod.ShellSeguro,
                     ComputerUseMethod.Portapapeles
                 })
        {
            var goal = ComputerUseGoals.ForMethod(method);
            Assert.NotNull(goal);
            Assert.Contains(method, ComputerUseGoals.CapableMethods(goal!.Value));
        }
    }
}
