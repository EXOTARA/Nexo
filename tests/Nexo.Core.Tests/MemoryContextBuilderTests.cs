using Nexo.Core.Memory;

namespace Nexo.Core.Tests;

/// <summary>
/// Diseño D10 (Fase 6) — este bloque SALE del equipo cuando el proveedor de IA es remoto, así que
/// se prueba como un envío: qué se incluye, qué se queda fuera y qué pasa cuando algo cambia.
/// </summary>
public sealed class MemoryContextBuilderTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 8, 1, 12, 0, 0, TimeSpan.FromHours(-6));

    private static MemoryEntry Entry(MemoryCategory category, string text, int daysAgo = 0) => new()
    {
        Category = category,
        Text = text,
        CreatedAt = Now.AddDays(-daysAgo)
    };

    private static MemorySettings AllOn() => new()
    {
        Enabled = true,
        RememberPreferences = true,
        RememberConversation = true,
        RememberHabits = true,
        RetentionDays = 30
    };

    [Fact]
    public void WithMemoryDisabled_NothingIsSent()
    {
        var entries = new[] { Entry(MemoryCategory.Preferencias, "prefiero respuestas cortas") };

        Assert.Null(MemoryContextBuilder.Build(entries, new MemorySettings { Enabled = false }, Now));
    }

    [Fact]
    public void EnabledCategories_AreIncluded()
    {
        var entries = new[] { Entry(MemoryCategory.Preferencias, "prefiero respuestas cortas") };

        var context = MemoryContextBuilder.Build(entries, AllOn(), Now);

        Assert.NotNull(context);
        Assert.Contains("prefiero respuestas cortas", context);
    }

    [Fact]
    public void DisablingACategory_StopsItsEntriesFromBeingSent_Immediately()
    {
        // La entrada sigue guardada — borrarla por mover un interruptor sería destruir datos, y
        // para eso está "olvidar" — pero deja de usarse en el acto.
        var entries = new[] { Entry(MemoryCategory.Conversacion, "el proyecto se llama Kohana") };
        var settings = AllOn();
        settings.RememberConversation = false;

        Assert.Null(MemoryContextBuilder.Build(entries, settings, Now));
    }

    [Fact]
    public void ExpiredEntries_AreNotSent()
    {
        var entries = new[] { Entry(MemoryCategory.Preferencias, "prefiero respuestas cortas", daysAgo: 40) };
        var settings = AllOn();
        settings.RetentionDays = 7;

        Assert.Null(MemoryContextBuilder.Build(entries, settings, Now));
    }

    [Fact]
    public void SensitiveEntries_AreNotSent_EvenIfStored()
    {
        // Defensa repetida a propósito: lo guardado ya pasó por la política, pero una entrada
        // escrita por una versión anterior con criterios más laxos no debe salir del equipo.
        var entries = new[] { Entry(MemoryCategory.Conversacion, "mi contraseña es Sakura2026") };

        Assert.Null(MemoryContextBuilder.Build(entries, AllOn(), Now));
    }

    [Fact]
    public void TheContextIsBounded_InCountAndSize()
    {
        var entries = Enumerable
            .Range(0, 60)
            .Select(index => Entry(MemoryCategory.Conversacion, $"dato numero {index}", daysAgo: index % 5))
            .ToArray();

        var context = MemoryContextBuilder.Build(entries, AllOn(), Now);

        Assert.NotNull(context);
        Assert.True(context!.Length <= MemoryContextBuilder.MaximumCharacters + 200);

        var lines = context.Split('\n').Count(line => line.TrimStart().StartsWith('-'));
        Assert.True(lines <= MemoryContextBuilder.MaximumEntries);
    }

    [Fact]
    public void WithNothingUsable_NoHeaderIsSentEither()
    {
        // Un encabezado solo anunciaría una memoria vacía: mejor no mandar nada que una promesa
        // sin contenido.
        Assert.Null(MemoryContextBuilder.Build([], AllOn(), Now));
    }
}
