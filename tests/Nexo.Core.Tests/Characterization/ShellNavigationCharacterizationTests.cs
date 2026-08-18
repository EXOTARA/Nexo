using Nexo.Core.Commands;
using Nexo.Core.Shell;

namespace Nexo.Core.Tests.Characterization;

/// <summary>
/// Fase 1.1 — congela la conducta de navegación del shell tal como existe hoy en
/// <c>MainWindow</c>, antes de extraer el Runtime (ADR 0001).
///
/// Estas pruebas describen lo que Kohana **hace ahora**, no lo que debería hacer. Si un paso
/// posterior de la extracción las rompe, la extracción cambió conducta y debe corregirse el
/// código, no la prueba (regla de `TEST_MATRIX.md`).
/// </summary>
public sealed class ShellNavigationCharacterizationTests
{
    [Fact]
    public void KnownDestinations_MatchTheNineRegisteredViews()
    {
        Assert.Equal(
            [
                "Home",
                "Assistant",
                "Tasks",
                "Focus",
                "Routines",
                "Audio",
                "Capture",
                "System",
                "Settings"
            ],
            ShellNavigationPolicy.KnownDestinations);
    }

    /// <summary>
    /// Diseño D52 — cambiada a propósito, no arreglada.
    ///
    /// Esta prueba congelaba «Inicio» como destino de arranque, y cumplió su papel: al reorganizar
    /// el shell falló y obligó a declarar el cambio en vez de dejarlo pasar. Kohana arranca ahora
    /// en el chat, porque el chat es la aplicación y abrir en un tablero de tarjetas la presentaba
    /// como otra cosa.
    /// </summary>
    [Fact]
    public void DefaultDestination_IsTheChat()
    {
        Assert.Equal("Assistant", ShellNavigationPolicy.DefaultDestination);
    }

    [Theory]
    [InlineData("Home")]
    [InlineData("home")]
    [InlineData("SETTINGS")]
    [InlineData("aSsIsTaNt")]
    public void Destinations_AreCaseInsensitive(string destination)
    {
        Assert.True(ShellNavigationPolicy.IsKnownDestination(destination));
    }

    [Theory]
    [InlineData("Memory")]
    [InlineData("Skills")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void UnknownDestination_IsRejected(string? destination)
    {
        // MainWindow.NavigateTo hace `TryGetValue` y regresa sin navegar: un destino
        // desconocido se ignora en silencio, no lanza ni cae a un destino por defecto.
        Assert.False(ShellNavigationPolicy.IsKnownDestination(destination));
    }

    [Fact]
    public void SettingsButton_EntersSettingsFromAnyOtherDestination()
    {
        Assert.Equal(
            "Settings",
            ShellNavigationPolicy.ResolveSettingsToggle("Tasks", "Home"));
    }

    [Fact]
    public void SettingsButton_RemembersTheDestinationItCameFrom()
    {
        Assert.Equal(
            "Tasks",
            ShellNavigationPolicy.ResolvePreviousDestination("Tasks", "Home"));
    }

    [Fact]
    public void SettingsButton_TogglesBackToThePreviousDestination()
    {
        Assert.Equal(
            "Tasks",
            ShellNavigationPolicy.ResolveSettingsToggle("Settings", "Tasks"));
    }

    [Fact]
    public void SettingsButton_DoesNotOverwriteThePreviousDestinationWhileInSettings()
    {
        // Pulsar Ajustes dos veces seguidas debe volver al origen, no quedarse anclado
        // en Ajustes.
        Assert.Equal(
            "Tasks",
            ShellNavigationPolicy.ResolvePreviousDestination("Settings", "Tasks"));
    }

    /// <summary>
    /// Diseño D52 — cambiada a propósito, y esta es la que más importaba de las tres.
    ///
    /// Salir de Personalizar sin un destino anterior usable caía en «Inicio». Con el rail reducido,
    /// Inicio ya no está en el rail: la salida habría dejado a la persona en una pantalla sin
    /// ninguna pestaña marcada, sin forma evidente de volver. Ahora cae en el chat, que es donde
    /// siempre hay algo que hacer y siempre está visible.
    /// </summary>
    [Fact]
    public void SettingsToggle_FallsBackToTheChatWhenPreviousIsUnusable()
    {
        Assert.Equal(
            "Assistant",
            ShellNavigationPolicy.ResolveSettingsToggle("Settings", "Memory"));
        Assert.Equal(
            "Assistant",
            ShellNavigationPolicy.ResolveSettingsToggle("Settings", null));
    }

    [Fact]
    public void TheFallbackIsAlwaysAModuleThatCannotLeaveTheRail()
    {
        // La regla que ata las dos decisiones: a donde se cae tiene que ser algo que siempre esté
        // visible. Si mañana alguien hace opcional el destino por omisión, esto falla y avisa.
        Assert.False(ShellNavigationPolicy.IsOptional(ShellNavigationPolicy.DefaultDestination));
        Assert.False(ShellNavigationPolicy.IsOptional(ShellNavigationPolicy.FallbackDestination));
    }

    [Fact]
    public void HidingTheActiveModule_FallsBackToAssistant()
    {
        var redirected = ShellNavigationPolicy.TryResolveHiddenModuleFallback(
            module: "Audio",
            visible: false,
            currentDestination: "Audio",
            out var fallback);

        Assert.True(redirected);
        Assert.Equal("Assistant", fallback);
    }

    [Fact]
    public void HidingAnInactiveModule_DoesNotNavigate()
    {
        var redirected = ShellNavigationPolicy.TryResolveHiddenModuleFallback(
            module: "Audio",
            visible: false,
            currentDestination: "Tasks",
            out _);

        Assert.False(redirected);
    }

    [Fact]
    public void ShowingAModule_NeverNavigates()
    {
        var redirected = ShellNavigationPolicy.TryResolveHiddenModuleFallback(
            module: "Audio",
            visible: true,
            currentDestination: "Audio",
            out _);

        Assert.False(redirected);
    }

    /// <summary>
    /// Diseño D52 — cambiada a propósito. Antes solo Audio, Captura y Sistema se podían quitar del
    /// rail; ahora se puede quitar casi todo.
    /// </summary>
    [Fact]
    public void EverythingButTheChatAndSettingsCanLeaveTheRail()
    {
        Assert.Equal(
            ["Home", "Tasks", "Focus", "Routines", "Audio", "Capture", "System"],
            ShellNavigationPolicy.OptionalModules);

        // El chat es la aplicación y Personalizar es desde donde vuelven los demás: quitar ese
        // sería cerrar la puerta desde dentro.
        Assert.False(ShellNavigationPolicy.IsOptional("Assistant"));
        Assert.False(ShellNavigationPolicy.IsOptional("Settings"));
    }

    [Fact]
    public void ANewInstallShowsOnlyTheChatSystemAndSettings()
    {
        Assert.Equal(
            ["Assistant", "System", "Settings"],
            ShellNavigationPolicy.DefaultVisibleModules);
    }

    [Fact]
    public void WhatLeavesTheRail_IsStillAKnownDestination()
    {
        // Quitar del rail no es quitar de la aplicación: cada módulo conserva su comando «Ir a…»
        // en la paleta, y eso solo funciona si sigue siendo un destino que el shell reconoce.
        Assert.All(
            ShellNavigationPolicy.OptionalModules,
            module => Assert.True(ShellNavigationPolicy.IsKnownDestination(module)));
    }

    [Theory]
    [InlineData(LocalCommandType.NavigateAssistant, "Assistant")]
    [InlineData(LocalCommandType.NavigateAudio, "Audio")]
    [InlineData(LocalCommandType.NavigateCapture, "Capture")]
    [InlineData(LocalCommandType.NavigateSystem, "System")]
    [InlineData(LocalCommandType.NavigateSettings, "Settings")]
    public void NavigationCommands_MapToTheirDestination(
        LocalCommandType commandType,
        string expectedDestination)
    {
        Assert.Equal(
            expectedDestination,
            ShellNavigationPolicy.ResolveNavigationCommand(commandType));
    }

    [Theory]
    [InlineData(LocalCommandType.OpenPowerShell)]
    [InlineData(LocalCommandType.ShowPeek)]
    [InlineData(LocalCommandType.None)]
    public void NonNavigationCommands_HaveNoDestination(LocalCommandType commandType)
    {
        Assert.Null(ShellNavigationPolicy.ResolveNavigationCommand(commandType));
    }

    [Fact]
    public void NoVoiceCommandNavigatesToHomeTasksFocusOrRoutines()
    {
        // Congela una asimetría real: hoy solo cinco destinos son alcanzables por orden de voz
        // directa. Tasks, Focus y Routines se alcanzan por sus propios parsers de dominio.
        var reachableByNavigationCommand = Enum.GetValues<LocalCommandType>()
            .Select(ShellNavigationPolicy.ResolveNavigationCommand)
            .Where(destination => destination is not null)
            .ToArray();

        Assert.DoesNotContain("Home", reachableByNavigationCommand);
        Assert.DoesNotContain("Tasks", reachableByNavigationCommand);
        Assert.DoesNotContain("Focus", reachableByNavigationCommand);
        Assert.DoesNotContain("Routines", reachableByNavigationCommand);
    }
}
