using Nexo.Core.Shell;

namespace Nexo.Core.Tests;

/// <summary>
/// Diseño D58 — lo que se abre por roce tiene que saber irse solo.
///
/// Adler lo dijo así: pasar el ratón sin querer deja Kohana abierta «a pesar de que ya no pasó
/// nada», y eso es invasivo.
/// </summary>
public sealed class UnattendedRevealPolicyTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 18, 4, 0, 0, TimeSpan.Zero);

    private static DateTimeOffset LeftAgo(TimeSpan ago) => Now - ago;

    [Fact]
    public void AfterTheGrace_WithNobodyThere_ItRetracts() =>
        Assert.True(UnattendedRevealPolicy.ShouldRetract(
            openedByHover: true,
            outsideSince: LeftAgo(TimeSpan.FromSeconds(3)),
            Now,
            hasUserAttention: false));

    [Fact]
    public void WithinTheGrace_ItStays()
    {
        // Salirse un momento a por el teclado no es abandonar la ventana.
        Assert.False(UnattendedRevealPolicy.ShouldRetract(
            openedByHover: true,
            outsideSince: LeftAgo(TimeSpan.FromSeconds(1)),
            Now,
            hasUserAttention: false));
    }

    [Fact]
    public void WithThePointerInside_ItStays() =>
        Assert.False(UnattendedRevealPolicy.ShouldRetract(
            openedByHover: true,
            outsideSince: null,
            Now,
            hasUserAttention: false));

    [Fact]
    public void ReadingWithoutTouchingTheMouse_IsNotAbandonment()
    {
        // El caso que hace que una retirada automática sea peor que el fallo: una respuesta larga
        // se lee con las manos quietas, y cerrarla a media lectura es imperdonable.
        Assert.False(UnattendedRevealPolicy.ShouldRetract(
            openedByHover: true,
            outsideSince: LeftAgo(TimeSpan.FromMinutes(5)),
            Now,
            hasUserAttention: true));
    }

    [Fact]
    public void WhatWasOpenedOnPurpose_StaysOnPurpose()
    {
        // Atajo o icono de la bandeja: retirar algo que se acaba de pedir es desobedecer.
        Assert.False(UnattendedRevealPolicy.ShouldRetract(
            openedByHover: false,
            outsideSince: LeftAgo(TimeSpan.FromMinutes(5)),
            Now,
            hasUserAttention: false));
    }

    [Fact]
    public void AClockThatWentBackwards_IsNotAnElapsedWait() =>
        Assert.False(UnattendedRevealPolicy.ShouldRetract(
            openedByHover: true,
            outsideSince: Now + TimeSpan.FromSeconds(30),
            Now,
            hasUserAttention: false));
}
